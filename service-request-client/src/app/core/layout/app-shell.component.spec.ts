import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthenticatedUser } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AppShellComponent } from './app-shell.component';

const agentUser: AuthenticatedUser = {
  id: 1,
  username: 'jane.doe',
  displayName: 'Jane Doe',
  email: 'jane.doe@example.test',
  role: 'SupportAgent',
};

const adminUser: AuthenticatedUser = {
  id: 2,
  username: 'admin',
  displayName: 'Admin User',
  email: 'admin@example.test',
  role: 'Admin',
};

const employeeUser: AuthenticatedUser = {
  id: 3,
  username: 'employee',
  displayName: 'Employee User',
  email: 'employee@example.test',
  role: 'Employee',
};

function createComponent(user: AuthenticatedUser, logoutSpy: jasmine.Spy): ComponentFixture<AppShellComponent> {
  TestBed.configureTestingModule({
    imports: [AppShellComponent],
    providers: [
      provideRouter([]),
      {
        provide: AuthService,
        useValue: {
          currentUser: () => user,
          hasRole: (role: string) => user.role === role,
          hasAnyRole: (roles: string[]) => roles.includes(user.role),
          logout: logoutSpy,
        },
      },
    ],
  });

  const fixture = TestBed.createComponent(AppShellComponent);
  fixture.detectChanges();
  return fixture;
}

describe('AppShellComponent', () => {
  let logoutSpy: jasmine.Spy;

  beforeEach(() => {
    logoutSpy = jasmine.createSpy('logout');
  });

  describe('SupportAgent user', () => {
    let fixture: ComponentFixture<AppShellComponent>;

    beforeEach(() => {
      fixture = createComponent(agentUser, logoutSpy);
    });

    it('shows the current user display name', () => {
      const nameElement = fixture.nativeElement.querySelector('.app-shell__user-name');
      expect(nameElement.textContent).toContain('Jane Doe');
    });

    it('shows the current user role', () => {
      const roleElement = fixture.nativeElement.querySelector('.app-shell__user-role');
      expect(roleElement.textContent).toContain('SupportAgent');
    });

    it('renders Dashboard and Requests nav links', () => {
      const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.app-shell__nav a');
      const texts = Array.from(links).map((l) => l.textContent?.trim());
      expect(texts).toContain('Dashboard');
      expect(texts).toContain('Requests');
    });

    it('does not render the Categories nav link for SupportAgent', () => {
      const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.app-shell__nav a');
      const texts = Array.from(links).map((l) => l.textContent?.trim());
      expect(texts).not.toContain('Categories');
    });

    it('calls AuthService.logout when the logout button is clicked', () => {
      const button: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="button"]');
      button.click();
      expect(logoutSpy).toHaveBeenCalled();
    });
  });

  describe('Admin user', () => {
    let fixture: ComponentFixture<AppShellComponent>;

    beforeEach(() => {
      fixture = createComponent(adminUser, logoutSpy);
    });

    it('renders the Categories nav link for Admin', () => {
      const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.app-shell__nav a');
      const texts = Array.from(links).map((l) => l.textContent?.trim());
      expect(texts).toContain('Categories');
    });

    it('renders Dashboard and Requests nav links for Admin', () => {
      const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.app-shell__nav a');
      const texts = Array.from(links).map((l) => l.textContent?.trim());
      expect(texts).toContain('Dashboard');
      expect(texts).toContain('Requests');
    });
  });

  describe('Employee user', () => {
    let fixture: ComponentFixture<AppShellComponent>;

    beforeEach(() => {
      fixture = createComponent(employeeUser, logoutSpy);
    });

    it('does not render the Categories nav link for Employee', () => {
      const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.app-shell__nav a');
      const texts = Array.from(links).map((l) => l.textContent?.trim());
      expect(texts).not.toContain('Categories');
    });

    it('renders Dashboard and Requests nav links for Employee', () => {
      const links: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.app-shell__nav a');
      const texts = Array.from(links).map((l) => l.textContent?.trim());
      expect(texts).toContain('Dashboard');
      expect(texts).toContain('Requests');
    });
  });
});
