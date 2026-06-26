// MIGRATION: Spec for ImportExportComponent (the Angular 19 replacement for DNN Export.ascx.vb + Import.ascx.vb).
// Verifies the export validation gate (folder placeholder / empty file -> "Validation" message, Export.ascx.vb
// L121/L132), the cleanName bad-char stripping + content.<...>.<...>.xml composition (Export.ascx.vb L124 /
// L209-221), the exportContent payload, the import validation gate (file required -> "Please specify the file to
// import", Import.ascx.vb L145/L157), the importContent payload, and RFC 7807 error-message parity. Gate 4:
// ng test --watch=false --browsers=ChromeHeadless --code-coverage (100% pass, non-interactive).
import { signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { ImportExportComponent } from './import-export.component';
import { ModuleService } from '../module.service';
import type { ModuleExportRequest, ModuleImportRequest } from '../module.service';
import type { Module } from '../../../core/models';

function makeModule(overrides: Partial<Module> = {}): Module {
  const base = {
    moduleId: 5,
    portalId: 1,
    tabId: 10,
    moduleOrder: 1,
    moduleTitle: 'Test Module',
    moduleName: 'Test Module',
    cacheTime: 0,
    allTabs: false,
    visibility: 0,
    isDeleted: false,
    displayTitle: true,
    displayPrint: true,
    displaySyndicate: false,
    inheritViewPermissions: true,
    defaultCacheTime: 0,
    controlType: 0,
  };
  return { ...base, ...overrides } as Module;
}

describe('ImportExportComponent', () => {
  let fixture: ComponentFixture<ImportExportComponent>;
  let component: ImportExportComponent;
  let selected: WritableSignal<Module | null>;
  let loading: WritableSignal<boolean>;
  let getByIdSpy: jasmine.Spy;
  let exportSpy: jasmine.Spy;
  let importSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    selected = signal<Module | null>(makeModule());
    loading = signal(false);
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(makeModule()));
    exportSpy = jasmine.createSpy('exportContent').and.returnValue(of(undefined));
    importSpy = jasmine.createSpy('importContent').and.returnValue(of(undefined));

    const moduleServiceStub = {
      selected,
      loading,
      getById: getByIdSpy,
      exportContent: exportSpy,
      importContent: importSpy,
    };

    TestBed.configureTestingModule({
      imports: [ImportExportComponent],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: ModuleService, useValue: moduleServiceStub },
      ],
    });

    fixture = TestBed.createComponent(ImportExportComponent);
    component = fixture.componentInstance;
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.componentRef.setInput('id', '5');
    fixture.detectChanges();
  });

  it('creates the component and pre-loads the module', () => {
    expect(component).toBeTruthy();
    expect(getByIdSpy).toHaveBeenCalledWith(5);
  });

  it('cleanName strips the exact legacy bad-char set', () => {
    const dirty = 'a.b c~`!@#$%^&*()-_+={[}]|\\:;<,>?/"\'z';
    expect(component.cleanName(dirty)).toBe('abcz');
  });

  it('buildExportFileName composes content.<CleanName(ModuleName)>.<CleanName(file)>.xml', () => {
    expect(component.buildExportFileName('Backup')).toBe('content.TestModule.Backup.xml');
  });

  it('blocks export when the folder is the placeholder and surfaces the Validation message', () => {
    component.exportForm.controls.fileName.setValue('Backup');
    // folder remains the default placeholder '-'
    component.submitExport();

    expect(exportSpy).not.toHaveBeenCalled();
    expect(component.formError()).toBe('You must specify a folder and file for export');
  });

  it('blocks export when the file name is empty', () => {
    component.exportForm.controls.folder.setValue('Content');
    component.exportForm.controls.fileName.setValue('');
    component.submitExport();

    expect(exportSpy).not.toHaveBeenCalled();
    expect(component.formError()).toBe('You must specify a folder and file for export');
  });

  it('exports with the composed payload when valid', () => {
    component.exportForm.controls.folder.setValue('Content');
    component.exportForm.controls.fileName.setValue('Backup');
    component.submitExport();

    expect(exportSpy).toHaveBeenCalledTimes(1);
    const args = exportSpy.calls.mostRecent().args;
    expect(args[0]).toBe(5);
    const payload = args[1] as ModuleExportRequest;
    expect(payload.folder).toBe('Content');
    expect(payload.fileName).toBe('content.TestModule.Backup.xml');
    expect(navigateSpy).toHaveBeenCalledWith(['/modules', '5', 'settings']);
  });

  it('blocks import when no file is specified and surfaces the legacy message', () => {
    component.setMode('import');
    component.importForm.controls.fileName.setValue('');
    component.submitImport();

    expect(importSpy).not.toHaveBeenCalled();
    expect(component.formError()).toBe('Please specify the file to import');
  });

  it('imports with the payload when a file is specified', () => {
    component.setMode('import');
    component.importForm.controls.folder.setValue('Content');
    component.importForm.controls.fileName.setValue('content.TestModule.Backup.xml');
    component.submitImport();

    expect(importSpy).toHaveBeenCalledTimes(1);
    const args = importSpy.calls.mostRecent().args;
    expect(args[0]).toBe(5);
    const payload = args[1] as ModuleImportRequest;
    expect(payload.folder).toBe('Content');
    expect(payload.fileName).toBe('content.TestModule.Backup.xml');
  });

  it('maps a server-side ProblemDetails error into the problem signal (export)', () => {
    const problem = {
      type: 'https://httpstatuses.io/400',
      title: 'Export failed',
      status: 400,
      detail: 'The module specified does not have any content',
    };
    exportSpy.and.returnValue(throwError(() => new HttpErrorResponse({ error: problem, status: 400 })));

    component.exportForm.controls.folder.setValue('Content');
    component.exportForm.controls.fileName.setValue('Backup');
    component.submitExport();

    expect(component.problem()?.status).toBe(400);
    expect(component.formError()).toBe('The module specified does not have any content');
    expect(component.submitting()).toBe(false);
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
