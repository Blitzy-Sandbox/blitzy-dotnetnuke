// MIGRATION: Angular 19 replacement for the legacy DotNetNuke module content import/export controls
// (Website/admin/Modules/Export.ascx.vb [227 lines] + Import.ascx.vb [245 lines]). Only the validation +
// orchestration logic is re-expressed here; ALL Web Forms postback/ViewState/PortalModuleBase/
// Framework.Reflection/Skin.AddModuleMessage/.resx/server-file-IO machinery is discarded. The actual IPortable
// ExportModule/ImportModule, the <content type version> XML wrap, the PortalController.HasSpaceAvailable
// disk-quota check, the clean-name file match, the XML LoadXml/type-attribute validation, and the file
// write/registration ALL execute SERVER-SIDE — documented below as // MIGRATION: notes and NOT implemented
// client-side. This single component hosts BOTH the export workflow (Export.ascx.vb cmdExport_Click L119-137 /
// ExportModule L143-207) and the import workflow (Import.ascx.vb cmdImport_Click L143-163 / ImportModule
// L169-223), toggled by a `mode` signal so only one form is in the DOM at a time.
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import type { AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';

import { ModuleService } from '../module.service';
import type { ModuleExportRequest, ModuleImportRequest } from '../module.service';
import { FormControlComponent } from '../../../shared/components/form-controls/form-control.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { Module, ProblemDetails } from '../../../core/models';

// MIGRATION: Export.ascx.vb Page_Load L70 / Import.ascx.vb Page_Load — the folder dropdown's first item is the
// "<None Specified>" placeholder with value "-"; cmdExport_Click L121 treats SelectedIndex <> 0 (i.e. NOT this
// placeholder) as "a real folder is selected". With no folder-enumeration endpoint (documented gap below), the
// folder is a free-text input and the placeholder value is preserved as the "no folder selected" sentinel.
const FOLDER_PLACEHOLDER = '-';

// MIGRATION: Export.ascx.vb CleanName L209-221. Verbatim legacy bad-char set:
//   strBadChars = ". ~`!@#$%^&*()-_+={[}]|\:;<,>?/" & Chr(34) & Chr(39)
// Chr(34) is the double-quote ("), Chr(39) is the single-quote ('). 33 characters total, INCLUDING a leading
// period and a space. Each character is removed (all occurrences) from the name.
const CLEAN_NAME_BAD_CHARS = '. ~`!@#$%^&*()-_+={[}]|\\:;<,>?/"\'';

// MIGRATION: Export.ascx.vb cmdExport_Click L132 -> Localization.GetString("Validation", LocalResourceFile).
const EXPORT_VALIDATION_MESSAGE = 'You must specify a folder and file for export';

// MIGRATION: Import.ascx.vb cmdImport_Click L157 -> hardcoded literal (NOT a .resx key in the legacy source).
const IMPORT_VALIDATION_MESSAGE = 'Please specify the file to import';

// MIGRATION: Export.ascx.vb ExportModule L198 -> Localization.GetString("Error") fallback.
const EXPORT_ERROR_MESSAGE = 'An error occurred during the export';

// MIGRATION: Import.ascx.vb ImportModule L211 -> Localization.GetString("Error") fallback.
const IMPORT_ERROR_MESSAGE = 'An error occurred during the import';

// MIGRATION: Export.ascx.vb cmdExport_Click L121 -> `cboFolders.SelectedIndex <> 0`. A real folder must be
// selected: reject BOTH an empty value AND the "<None Specified>" placeholder ("-"). Declared at module scope to
// avoid `this` binding inside the typed FormControl validator.
function realFolderValidator(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  return value && value !== FOLDER_PLACEHOLDER ? null : { folderRequired: true };
}

type WorkflowMode = 'export' | 'import';

// MIGRATION: typed export form. folder <- legacy cboFolders.SelectedItem.Value; fileName <- legacy txtFile.Text.
interface ExportFormModel {
  folder: FormControl<string>;
  fileName: FormControl<string>;
}

// MIGRATION: typed import form. folder <- legacy cboFolders.SelectedItem.Value; fileName <- legacy
// cboFiles.SelectedItem.Value (the full XML file name).
interface ImportFormModel {
  folder: FormControl<string>;
  fileName: FormControl<string>;
}

@Component({
  selector: 'app-import-export',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormControlComponent, LoadingSpinnerComponent],
  templateUrl: './import-export.component.html',
  styleUrl: './import-export.component.scss',
})
export class ImportExportComponent {
  private readonly moduleService = inject(ModuleService);
  private readonly router = inject(Router);

  // MIGRATION: withComponentInputBinding() (app.config.ts) binds the :id route param to this input. The legacy
  // controls read the target module via the ModuleId query string; here it arrives as the :id route param
  // (STRING) and is parsed to a number for moduleService calls (ModuleService.getById(id: number)).
  readonly id = input.required<string>();

  // The module's name/friendlyName/businessControllerClass context, pre-loaded for the clean-name + portability
  // logic (legacy Export/Import GetModule(ModuleId, TabId, False)).
  readonly module = this.moduleService.selected;
  readonly loading = this.moduleService.loading;

  // MIGRATION: the two legacy controls (Export.ascx / Import.ascx) are unified into one component with a mode
  // toggle. Only one form is rendered at a time (template @switch) so FormControlComponent never emits duplicate
  // element ids for the shared 'folder'/'fileName' field keys.
  readonly mode = signal<WorkflowMode>('export');

  readonly submitting = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly problem = signal<ProblemDetails | null>(null);

  // MIGRATION: form-level (non-field) message surface. Holds the legacy "Validation" / "Please specify the file"
  // strings and any RFC 7807 ProblemDetails.detail/title fallback (legacy Skin.AddModuleMessage RedError).
  readonly formError = signal<string | null>(null);

  // MIGRATION: surface a flat string[] ProblemDetails.errors payload as a form-level summary;
  // <app-form-control> only renders the per-field Record<string,string[]> shape.
  readonly errorSummary = computed<string[]>(() => {
    const errors = this.problem()?.errors;
    return Array.isArray(errors) ? errors : [];
  });

  // MIGRATION: per-field RFC 7807 errors (Record<string,string[]>) passed to each <app-form-control>; flat-array
  // payloads are surfaced via errorSummary instead.
  readonly fieldErrors = computed<Record<string, string[]> | undefined>(() => {
    const errors = this.problem()?.errors;
    return errors && !Array.isArray(errors) ? errors : undefined;
  });

  // MIGRATION: Export.ascx.vb cmdExport_Click L121 — folder (real, not "-" placeholder) AND non-empty file name.
  readonly exportForm: FormGroup<ExportFormModel> = new FormGroup<ExportFormModel>({
    folder: new FormControl(FOLDER_PLACEHOLDER, {
      nonNullable: true,
      validators: [realFolderValidator],
    }),
    fileName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  // MIGRATION: Import.ascx.vb cmdImport_Click L145 — only the FILE is the validation gate (a file must be
  // specified). The folder is sent in the payload but, matching the legacy, does NOT block submission.
  readonly importForm: FormGroup<ImportFormModel> = new FormGroup<ImportFormModel>({
    folder: new FormControl(FOLDER_PLACEHOLDER, { nonNullable: true }),
    fileName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  constructor() {
    // MIGRATION: Export/Import Page_Load -> GetModule(ModuleId, TabId, False) pre-load (Export.ascx.vb L82-86 /
    // Import.ascx.vb L75-79). Load the module so its ModuleName/FriendlyName/BusinessControllerClass context is
    // available for the clean-name + portability logic, and seed the export file name from
    // CleanName(ModuleTitle) (Export.ascx.vb L86). Runs in the constructor injection context; reading the
    // required `id` input inside the effect is safe because the router binds it before the first change
    // detection (and tests setInput() before detectChanges()).
    effect(() => {
      const moduleId = Number(this.id());
      this.moduleService.getById(moduleId).subscribe({
        next: (loaded) => this.seedExportFileName(loaded),
        error: () => {
          // The global error interceptor surfaces/logs the ProblemDetails; nothing else to do here.
        },
      });
    });
  }

  // MIGRATION: Export.ascx.vb Page_Load L86 -> txtFile.Text = CleanName(objModule.ModuleTitle). Only seed when
  // the user has not yet typed a file name (control still pristine and empty).
  private seedExportFileName(loaded: Module): void {
    const control = this.exportForm.controls.fileName;
    if (control.pristine && !control.value) {
      const title = loaded.moduleTitle ?? '';
      control.setValue(this.cleanName(title));
    }
  }

  setMode(mode: WorkflowMode): void {
    this.mode.set(mode);
    // Clear any cross-workflow messages when switching tabs.
    this.formError.set(null);
    this.successMessage.set(null);
    this.problem.set(null);
  }

  // MIGRATION: Export.ascx.vb CleanName L209-221 — removes every character in the legacy bad-char set (all
  // occurrences), faithfully reproducing the VB `For ... strName.Replace(char, "")` loop. Public so the spec can
  // assert exact parity. `split(ch).join('')` removes all occurrences without regex-escaping concerns.
  cleanName(name: string): string {
    let result = name;
    for (const badChar of CLEAN_NAME_BAD_CHARS) {
      result = result.split(badChar).join('');
    }
    return result;
  }

  // MIGRATION: Export.ascx.vb cmdExport_Click L124 — strFile = "content." & CleanName(ModuleName) & "." &
  // CleanName(txtFile.Text) & ".xml". NOTE: the PREFIX uses ModuleName (distinct from the ModuleTitle used to
  // seed the default file name at L86). Public so the spec can assert the exact composed name.
  buildExportFileName(rawFileName: string): string {
    const moduleName = this.module()?.moduleName ?? '';
    return `content.${this.cleanName(moduleName)}.${this.cleanName(rawFileName)}.xml`;
  }

  // MIGRATION: Export.ascx.vb cmdExport_Click L119-137. Legacy gated on cboFolders.SelectedIndex <> 0 AND
  // txtFile.Text <> "" (else the "Validation" message), composed the file name (L124), then called ExportModule.
  // Here we validate the reactive form, compose the legacy file name, and call moduleService.exportContent. The
  // SERVER performs the IPortable export, the <content> XML wrap, the HasSpaceAvailable disk-quota check, and the
  // file write/register (legacy ExportModule L143-207 — DiskSpaceExceeded/NoContent/ExportNotSupported/Error are
  // surfaced from the server via RFC 7807).
  submitExport(): void {
    this.resetMessages();
    if (this.exportForm.invalid) {
      this.exportForm.markAllAsTouched();
      // MIGRATION: Export.ascx.vb L132 -> "Validation" message.
      this.formError.set(EXPORT_VALIDATION_MESSAGE);
      return;
    }

    const { folder, fileName } = this.exportForm.getRawValue();
    // MIGRATION: send the legacy-composed file name (content.<CleanName(ModuleName)>.<CleanName(file)>.xml) as
    // the payload fileName. The legacy composed this in code-behind (Export.ascx.vb L124); the forward-looking
    // modules/{id}/export endpoint (documented gap) MUST treat it as the final name and NOT re-apply the
    // convention (ModuleExportRequest's doc-comment describes a raw "base name" — divergence recorded in
    // MIGRATION_NOTES.md).
    const payload: ModuleExportRequest = {
      folder,
      fileName: this.buildExportFileName(fileName),
    };

    this.submitting.set(true);
    this.moduleService.exportContent(Number(this.id()), payload).subscribe({
      next: () => {
        this.submitting.set(false);
        this.successMessage.set('Module content exported successfully.');
        // MIGRATION: legacy redirected to NavigateURL() on success; the SPA returns to the module settings view.
        void this.router.navigate(['/modules', this.id(), 'settings']);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.applyError(err, EXPORT_ERROR_MESSAGE);
      },
    });
  }

  // MIGRATION: Import.ascx.vb cmdImport_Click L143-163. Legacy gated on `Not cboFiles.SelectedItem Is Nothing`
  // (else "Please specify the file to import"), then called ImportModule. Here we validate the reactive form
  // (file required) and call moduleService.importContent. The SERVER performs the clean-name file match
  // (ImportModule L176), the XmlDocument.LoadXml + root `type`-attribute validation (L188-204), the version
  // extraction, and IPortable.ImportModule (L200) — NotValidXml/NotCorrectType/ImportNotSupported/Error are
  // surfaced from the server via RFC 7807.
  submitImport(): void {
    this.resetMessages();
    if (this.importForm.invalid) {
      this.importForm.markAllAsTouched();
      // MIGRATION: Import.ascx.vb L157 -> hardcoded "Please specify the file to import".
      this.formError.set(IMPORT_VALIDATION_MESSAGE);
      return;
    }

    const { folder, fileName } = this.importForm.getRawValue();
    // MIGRATION: Import.ascx.vb L149 -> ImportModule(ModuleId, cboFiles.SelectedItem.Value,
    // cboFolders.SelectedItem.Value). fileName is the full XML file name the user selected/entered.
    const payload: ModuleImportRequest = { folder, fileName };

    this.submitting.set(true);
    this.moduleService.importContent(Number(this.id()), payload).subscribe({
      next: () => {
        this.submitting.set(false);
        this.successMessage.set('Module content imported successfully.');
        void this.router.navigate(['/modules', this.id(), 'settings']);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.applyError(err, IMPORT_ERROR_MESSAGE);
      },
    });
  }

  cancel(): void {
    // MIGRATION: Export/Import cmdCancel_Click -> Response.Redirect(NavigateURL()). Returns to module settings.
    void this.router.navigate(['/modules', this.id(), 'settings']);
  }

  private resetMessages(): void {
    this.formError.set(null);
    this.successMessage.set(null);
    this.problem.set(null);
  }

  // MIGRATION: server-side validation/errors -> RFC 7807 ProblemDetails surfaced per field via
  // <app-form-control> [errors] (fieldErrors) and, for flat-array / form-level payloads, the formError /
  // errorSummary surfaces. Falls back to the legacy "Error" message when no ProblemDetails body is present.
  private applyError(err: unknown, fallbackMessage: string): void {
    const problem = this.toProblemDetails(err);
    this.problem.set(problem);
    const detail = problem?.detail ?? problem?.title;
    this.formError.set(detail ?? fallbackMessage);
  }

  // The global error interceptor re-throws the original HttpErrorResponse; the RFC 7807 body is on `error`.
  private toProblemDetails(err: unknown): ProblemDetails | null {
    if (err instanceof HttpErrorResponse) {
      const body: unknown = err.error;
      if (typeof body === 'object' && body !== null) {
        return body as ProblemDetails;
      }
    }
    return null;
  }
}
