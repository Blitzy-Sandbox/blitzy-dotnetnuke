import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FooterComponent } from './footer.component';

describe('FooterComponent', () => {
  let component: FooterComponent;
  let fixture: ComponentFixture<FooterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FooterComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FooterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render a semantic contentinfo footer landmark', () => {
    const element = fixture.nativeElement as HTMLElement;
    const footer = element.querySelector('footer');

    expect(footer).not.toBeNull();
    expect(footer?.getAttribute('role')).toBe('contentinfo');
  });

  it('should display the application name', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('DNN Migration');
  });

  it('should display the current calendar year', () => {
    const element = fixture.nativeElement as HTMLElement;
    const expectedYear = new Date().getFullYear().toString();

    expect(element.textContent).toContain(expectedYear);
  });
});
