import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ResendConfirmation } from './resend-confirmation';
import { AccountService } from '../account.service';

describe('ResendConfirmation', () => {
  function setup(resendConfirmation = vi.fn().mockReturnValue(of(undefined))) {
    TestBed.configureTestingModule({
      imports: [ResendConfirmation],
      providers: [{ provide: AccountService, useValue: { resendConfirmation } }],
    });
    const fixture = TestBed.createComponent(ResendConfirmation);
    fixture.componentRef.setInput('email', 'ada@example.com');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const click = () => {
      element.querySelector('button')!.click();
      fixture.detectChanges();
    };
    return { resendConfirmation, element, click };
  }

  it('sends the link to the given address and says so', () => {
    const { resendConfirmation, element, click } = setup();

    click();

    expect(resendConfirmation).toHaveBeenCalledWith('ada@example.com');
    expect(element.textContent).toContain('Sent. Check your inbox.');
  });

  it('says when the email could not be sent', () => {
    const { element, click } = setup(vi.fn().mockReturnValue(throwError(() => new Error('down'))));

    click();

    expect(element.textContent).toContain('The email could not be sent.');
  });
});
