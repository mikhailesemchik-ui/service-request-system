import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { AUTH_LOGIN_PATH, LoginResponse } from '../../../core/auth/auth.models';
import { LoginPageComponent } from './login.page';

const loginUrl = `${environment.apiBaseUrl}${AUTH_LOGIN_PATH}`;

const loginResponse: LoginResponse = {
  accessToken: 'jwt-token',
  tokenType: 'Bearer',
  expiresAt: '2026-01-01T00:00:00Z',
  user: { id: 1, username: 'jane.doe', displayName: 'Jane Doe', email: 'jane.doe@example.test', role: 'Employee' },
};

describe('LoginPageComponent', () => {
  let fixture: ComponentFixture<LoginPageComponent>;
  let component: LoginPageComponent;
  let httpMock: HttpTestingController;
  let router: Router;

  function createActivatedRouteStub(returnUrl: string | null = null) {
    return {
      snapshot: { queryParamMap: convertToParamMap(returnUrl ? { returnUrl } : {}) },
    };
  }

  function configure(returnUrl: string | null = null): void {
    TestBed.configureTestingModule({
      imports: [LoginPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: createActivatedRouteStub(returnUrl) },
      ],
    });

    fixture = TestBed.createComponent(LoginPageComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    // Prevent real navigation: with an empty route table, an un-spied navigateByUrl call can
    // resolve asynchronously after this spec's TestBed environment has already been torn down.
    spyOn(router, 'navigateByUrl');
    fixture.detectChanges();
  }

  afterEach(() => {
    httpMock.verify();
  });

  function setFormValues(username: string, password: string): void {
    component['form'].setValue({ username, password });
  }

  it('starts with an invalid form', () => {
    configure();

    expect(component['form'].invalid).toBeTrue();
  });

  it('marks username and password as required', () => {
    configure();

    expect(component['form'].controls.username.hasError('required')).toBeTrue();
    expect(component['form'].controls.password.hasError('required')).toBeTrue();
  });

  it('does not submit when the form is invalid', () => {
    configure();

    component['submit']();

    expect(component['isSubmitting']()).toBeFalse();
    httpMock.expectNone(loginUrl);
  });

  it('calls the login endpoint on a valid submit', () => {
    configure();
    setFormValues('jane.doe', 'Password123!');

    component['submit']();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    req.flush(loginResponse);
  });

  it('normalizes the submitted username by trimming surrounding whitespace', () => {
    configure();
    setFormValues('  jane.doe  ', 'Password123!');

    component['submit']();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.body.username).toBe('jane.doe');
    req.flush(loginResponse);
  });

  it('does not trim the submitted password', () => {
    configure();
    setFormValues('jane.doe', '  Password123!  ');

    component['submit']();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.body.password).toBe('  Password123!  ');
    req.flush(loginResponse);
  });

  it('disables submission while a login request is in flight', () => {
    configure();
    setFormValues('jane.doe', 'Password123!');

    component['submit']();
    fixture.detectChanges();

    expect(component['isSubmitting']()).toBeTrue();
    const button = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(button.disabled).toBeTrue();

    httpMock.expectOne(loginUrl).flush(loginResponse);
  });

  it('displays the backend error detail and re-enables submission on failure', () => {
    configure();
    setFormValues('jane.doe', 'WrongPassword!');

    component['submit']();
    httpMock
      .expectOne(loginUrl)
      .flush({ detail: 'Invalid username or password.' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(component['isSubmitting']()).toBeFalse();
    expect(component['errorMessage']()).toBe('Invalid username or password.');

    const errorElement = fixture.nativeElement.querySelector('.login-page__error');
    expect(errorElement.textContent).toContain('Invalid username or password.');
  });

  it('prevents a duplicate submission while one is already in flight', () => {
    configure();
    setFormValues('jane.doe', 'Password123!');

    component['submit']();
    component['submit']();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    req.flush(loginResponse);
  });

  it('navigates to /dashboard after a successful login with no return URL', () => {
    configure(null);
    setFormValues('jane.doe', 'Password123!');

    component['submit']();
    httpMock.expectOne(loginUrl).flush(loginResponse);

    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('navigates to a safe internal return URL after a successful login', () => {
    configure('/categories');
    setFormValues('jane.doe', 'Password123!');

    component['submit']();
    httpMock.expectOne(loginUrl).flush(loginResponse);

    expect(router.navigateByUrl).toHaveBeenCalledWith('/categories');
  });

  it('rejects an unsafe external return URL and falls back to /dashboard', () => {
    configure('//evil.example.test');
    setFormValues('jane.doe', 'Password123!');

    component['submit']();
    httpMock.expectOne(loginUrl).flush(loginResponse);

    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });
});
