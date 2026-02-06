/**
 * @fileoverview Unit Tests for AppComponent
 * @description Test suite for the root Angular 19 standalone application component.
 * Tests cover component creation, layout rendering, sidebar toggle functionality,
 * and integration with layout child components.
 *
 * MIGRATION NOTE: Tests verify that AppComponent correctly replaces the
 * legacy DNN Default.aspx WebForms master layout functionality.
 *
 * @module app.component.spec
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { By } from '@angular/platform-browser';
import { RouterModule } from '@angular/router';

import { AppComponent } from './app.component';

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        AppComponent,
        RouterModule
      ],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  describe('Component Creation', () => {
    it('should create the app', () => {
      expect(component).toBeTruthy();
    });

    it('should have the "DNN Migration" title', () => {
      expect(component.title).toEqual('DNN Migration');
    });

    it('should initialize with sidebar expanded (not collapsed)', () => {
      expect(component.isSidebarCollapsed()).toBeFalse();
    });
  });

  describe('Sidebar Toggle Functionality', () => {
    it('should toggle sidebar collapsed state when onSidebarToggle is called', () => {
      // Initial state - expanded
      expect(component.isSidebarCollapsed()).toBeFalse();

      // First toggle - collapse
      component.onSidebarToggle();
      expect(component.isSidebarCollapsed()).toBeTrue();

      // Second toggle - expand
      component.onSidebarToggle();
      expect(component.isSidebarCollapsed()).toBeFalse();
    });

    it('should apply collapsed class to sidebar when collapsed', () => {
      component.onSidebarToggle();
      fixture.detectChanges();

      const sidebar = fixture.debugElement.query(By.css('.app-sidebar'));
      expect(sidebar.classes['collapsed']).toBeTrue();
    });

    it('should remove collapsed class from sidebar when expanded', () => {
      // Collapse first
      component.onSidebarToggle();
      fixture.detectChanges();

      // Then expand
      component.onSidebarToggle();
      fixture.detectChanges();

      const sidebar = fixture.debugElement.query(By.css('.app-sidebar'));
      expect(sidebar.classes['collapsed']).toBeFalsy();
    });
  });

  describe('Template Structure', () => {
    it('should render the app container', () => {
      const container = fixture.debugElement.query(By.css('.app-container'));
      expect(container).toBeTruthy();
    });

    it('should render the header component', () => {
      const header = fixture.debugElement.query(By.css('app-header'));
      expect(header).toBeTruthy();
    });

    it('should render the sidebar navigation', () => {
      const sidebar = fixture.debugElement.query(By.css('.app-sidebar'));
      expect(sidebar).toBeTruthy();
      expect(sidebar.attributes['role']).toEqual('navigation');
    });

    it('should render the sidebar component', () => {
      const sidebarComponent = fixture.debugElement.query(By.css('app-sidebar'));
      expect(sidebarComponent).toBeTruthy();
    });

    it('should render the main content area', () => {
      const main = fixture.debugElement.query(By.css('.app-main'));
      expect(main).toBeTruthy();
      expect(main.attributes['role']).toEqual('main');
    });

    it('should render the router-outlet', () => {
      const routerOutlet = fixture.debugElement.query(By.css('router-outlet'));
      expect(routerOutlet).toBeTruthy();
    });

    it('should render the footer component', () => {
      const footer = fixture.debugElement.query(By.css('app-footer'));
      expect(footer).toBeTruthy();
    });
  });

  describe('Sidebar Toggle Button', () => {
    it('should have a sidebar toggle button', () => {
      const toggleButton = fixture.debugElement.query(By.css('.sidebar-toggle'));
      expect(toggleButton).toBeTruthy();
    });

    it('should toggle sidebar when toggle button is clicked', () => {
      const toggleButton = fixture.debugElement.query(By.css('.sidebar-toggle'));
      
      // Initial state
      expect(component.isSidebarCollapsed()).toBeFalse();

      // Click toggle
      toggleButton.nativeElement.click();
      fixture.detectChanges();

      expect(component.isSidebarCollapsed()).toBeTrue();
    });

    it('should have correct aria-expanded attribute based on collapsed state', () => {
      const toggleButton = fixture.debugElement.query(By.css('.sidebar-toggle'));
      
      // When expanded
      expect(toggleButton.attributes['aria-expanded']).toEqual('true');

      // When collapsed
      component.onSidebarToggle();
      fixture.detectChanges();
      expect(toggleButton.attributes['aria-expanded']).toEqual('false');
    });

    it('should have aria-controls pointing to sidebar-navigation', () => {
      const toggleButton = fixture.debugElement.query(By.css('.sidebar-toggle'));
      expect(toggleButton.attributes['aria-controls']).toEqual('sidebar-navigation');
    });

    it('should have an accessible aria-label', () => {
      const toggleButton = fixture.debugElement.query(By.css('.sidebar-toggle'));
      expect(toggleButton.attributes['aria-label']).toEqual('Toggle sidebar navigation');
    });
  });

  describe('Accessibility', () => {
    it('should have navigation role on sidebar', () => {
      const sidebar = fixture.debugElement.query(By.css('.app-sidebar'));
      expect(sidebar.attributes['role']).toEqual('navigation');
    });

    it('should have main role on content area', () => {
      const main = fixture.debugElement.query(By.css('.app-main'));
      expect(main.attributes['role']).toEqual('main');
    });

    it('should have aria-label on sidebar', () => {
      const sidebar = fixture.debugElement.query(By.css('.app-sidebar'));
      expect(sidebar.attributes['aria-label']).toEqual('Main navigation');
    });
  });

  describe('CSS Class Bindings', () => {
    it('should apply sidebar-collapsed class to container when sidebar is collapsed', () => {
      component.onSidebarToggle();
      fixture.detectChanges();

      const container = fixture.debugElement.query(By.css('.app-container'));
      expect(container.classes['sidebar-collapsed']).toBeTrue();
    });

    it('should not have sidebar-collapsed class when sidebar is expanded', () => {
      const container = fixture.debugElement.query(By.css('.app-container'));
      expect(container.classes['sidebar-collapsed']).toBeFalsy();
    });
  });

  describe('Signal State Management', () => {
    it('should use Angular signals for state management', () => {
      // Verify that isSidebarCollapsed is a signal (callable function)
      expect(typeof component.isSidebarCollapsed).toEqual('function');
      expect(typeof component.isSidebarCollapsed()).toEqual('boolean');
    });

    it('should maintain signal reactivity', () => {
      const initialValue = component.isSidebarCollapsed();
      component.onSidebarToggle();
      const updatedValue = component.isSidebarCollapsed();
      
      expect(initialValue).not.toEqual(updatedValue);
    });
  });
});
