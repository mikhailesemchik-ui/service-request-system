import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthenticatedUser } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AppShellComponent } from './app-shell.component';

const testUser: AuthenticatedUser = {
  id: 1,
  username: 'jane.doe',
  displayName: 'Jane Doe',
  email: 'jane.doe@example.test',
  role: 'SupportAgent',
};

describe('AppShellComponent', () => {
  let fixture: ComponentFixture<AppShellComponent>;
  let logoutSpy: jasmine.Spy;

  beforeEach(() => {
    logoutSpy = jasmine.createSpy('logout');

    TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser: () => testUser,
            logout: logoutSpy,
          },
        },
      ],
    });

    fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
  });

  it('shows the current user display name', () => {
    const nameElement = fixture.nativeElement.querySelector('.app-shell__user-name');
    expect(nameElement.textContent).toContain('Jane Doe');
  });

  it('shows the current user role', () => {
    const roleElement = fixture.nativeElement.querySelector('.app-shell__user-role');
    expect(roleElement.textContent).toContain('SupportAgent');
  });

  it('renders navigation links for Dashboard and Categories', () => {
    const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.app-shell__nav a');
    const linkTexts = Array.from(links).map((link) => link.textContent?.trim());

    expect(linkTexts).toContain('Dashboard');
    expect(linkTexts).toContain('Categories');
  });

  it('calls AuthService.logout when the logout button is clicked', () => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="button"]');
    button.click();

    expect(logoutSpy).toHaveBeenCalled();
  });
});
