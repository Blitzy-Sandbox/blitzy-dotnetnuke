/**
 * MIGRATION: RoleFormComponent (Stub for compilation)
 * 
 * Angular 19 standalone component for role create/edit form.
 * Full implementation pending - this is a minimal stub to enable routing.
 * 
 * MIGRATION SOURCE: Website/admin/Security/EditRoles.ascx.vb
 */

import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';

@Component({
  selector: 'app-role-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  template: `
    <div class="role-form-container">
      <h1>{{ isEditMode() ? 'Edit Role' : 'Add Role' }}</h1>
      
      @if (loading()) {
        <p>Loading...</p>
      } @else {
        <form [formGroup]="roleForm" (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label for="roleName">Role Name</label>
            <input 
              id="roleName" 
              type="text" 
              formControlName="roleName"
              class="form-control" />
          </div>
          
          <div class="form-group">
            <label for="description">Description</label>
            <textarea 
              id="description" 
              formControlName="description"
              class="form-control"></textarea>
          </div>
          
          <div class="form-actions">
            <button type="submit" class="btn btn-primary">Save</button>
            <button type="button" class="btn btn-secondary" (click)="onCancel()">Cancel</button>
          </div>
        </form>
      }
    </div>
  `,
  styles: [`
    .role-form-container {
      padding: 20px;
    }
    .form-group {
      margin-bottom: 16px;
    }
    .form-group label {
      display: block;
      margin-bottom: 4px;
      font-weight: 500;
    }
    .form-control {
      width: 100%;
      padding: 8px 12px;
      border: 1px solid #ccc;
      border-radius: 4px;
    }
    .form-actions {
      display: flex;
      gap: 8px;
      margin-top: 20px;
    }
    .btn {
      padding: 8px 16px;
      border: none;
      border-radius: 4px;
      cursor: pointer;
    }
    .btn-primary {
      background-color: #1976d2;
      color: white;
    }
    .btn-secondary {
      background-color: #6c757d;
      color: white;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoleFormComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal<boolean>(false);
  readonly roleId = signal<number | null>(null);

  readonly roleForm = new FormGroup({
    roleName: new FormControl('', Validators.required),
    description: new FormControl('')
  });

  isEditMode(): boolean {
    return this.roleId() !== null;
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.roleId.set(parseInt(idParam, 10));
      // TODO: Load role data for editing
    }
  }

  onSubmit(): void {
    if (this.roleForm.invalid) {
      return;
    }
    // TODO: Save role
    this.router.navigate(['/roles']);
  }

  onCancel(): void {
    this.router.navigate(['/roles']);
  }
}
