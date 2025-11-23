namespace Notifications.Domain.Enums;

public enum EmailTemplateType
{
    Generic = 0,
    VerificationLink = 1,
    VerificationCode = 2,
    MfaCode = 3,
    PasswordReset = 4
}