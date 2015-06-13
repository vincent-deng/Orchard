using System;
using System.Security.Cryptography.X509Certificates;
using IDeliverable.Licensing.VerificationTokens;

namespace IDeliverable.Licensing.Validation
{
    public class LicenseValidator
    {
        private static readonly TimeSpan _verificationTokenValidFor = TimeSpan.FromDays(21);

        public LicenseValidator(LicenseVerificationTokenAccessor verificationTokenAccessor)
        {
            _httpContextAccessor = new HttpContextAccessor();
            _verificationTokenAccessor = verificationTokenAccessor;
        }

        private readonly HttpContextAccessor _httpContextAccessor;
        private readonly LicenseVerificationTokenAccessor _verificationTokenAccessor;

        public void ValidateLicense(string productId, string licenseKey, LicenseValidationOptions options = LicenseValidationOptions.Default)
        {
            var request = _httpContextAccessor.Current().Request;

            var skipForLocalRequests = (options & LicenseValidationOptions.SkipForLocalRequests) == LicenseValidationOptions.SkipForLocalRequests;
            if (request.IsLocal && skipForLocalRequests)
                return;

            LicenseVerificationToken token = null;

            try
            {
                var forceRenewToken = (options & LicenseValidationOptions.ForceRenewToken) == LicenseValidationOptions.ForceRenewToken;
                token = _verificationTokenAccessor.GetLicenseVerificationToken(productId, licenseKey, request.GetHttpHost(), forceRenewToken);

                // If the token we got back is too old to be considered valid anymore, try to get the token
                // again, this time forcing token renewal.
                if (token.Age > _verificationTokenValidFor)
                    token = _verificationTokenAccessor.GetLicenseVerificationToken(productId, licenseKey, request.GetHttpHost(), forceRenew: true);
            }
            catch (LicenseVerificationTokenException ex)
            {
                LicenseValidationError error = LicenseValidationError.UnexpectedError;

                switch (ex.Error)
                {
                    case LicenseVerificationTokenError.UnknownLicenseKey:
                        error = LicenseValidationError.UnknownLicenseKey;
                        break;

                    case LicenseVerificationTokenError.HostnameMismatch:
                        error = LicenseValidationError.HostnameMismatch;
                        break;

                    case LicenseVerificationTokenError.LicenseServiceError:
                        error = LicenseValidationError.LicensingServiceError;
                        break;

                    case LicenseVerificationTokenError.LicenseServiceUnreachable:
                        error = LicenseValidationError.LicensingServiceUnreachable;
                        break;
                }

                throw new LicenseValidationException(ex.Message, error);
            }
            catch (Exception ex)
            {
                throw new LicenseValidationException("An unexpected error occurred while validating the license.", LicenseValidationError.UnexpectedError, ex);
            }

            // It should never happen that at this point we don't have a token or it's too old. 
            if (token == null || token.Age > _verificationTokenValidFor)
                throw new LicenseValidationException(LicenseValidationError.UnexpectedError);

            var signingCertificate = GetSigningCertificate();
            if (!token.GetSignatureIsValid(signingCertificate))
                throw new LicenseValidationException("License verification token signature validation failed.", LicenseValidationError.TokenSignatureValidationFailed);
        }

        private static X509Certificate2 GetSigningCertificate()
        {
            const string certBase64 =
@"-----BEGIN CERTIFICATE-----
MIIG0TCCBLmgAwIBAgIQ7qnUeLGos7hDPcw3o9G74jANBgkqhkiG9w0BAQ0FADCB
ozELMAkGA1UEBhMCQ1kxETAPBgNVBAgTCExpbWFzc29sMREwDwYDVQQHEwhMaW1h
c3NvbDEZMBcGA1UEChMQSURlbGl2ZXJhYmxlIEx0ZDETMBEGA1UECxMKT3BlcmF0
aW9uczEYMBYGA1UEAxMPSURlbGl2ZXJhYmxlIENBMSQwIgYJKoZIhvcNAQkBFhVp
bmZvQGlkZWxpdmVyYWJsZS5jb20wHhcNMTQxMjMxMjEwMDAwWhcNMjQxMjMxMjEw
MDAwWjCBrjELMAkGA1UEBhMCQ1kxETAPBgNVBAgTCExpbWFzc29sMREwDwYDVQQH
EwhMaW1hc3NvbDEZMBcGA1UEChMQSURlbGl2ZXJhYmxlIEx0ZDETMBEGA1UECxMK
T3BlcmF0aW9uczEjMCEGA1UEAxMabGljZW5zaW5nLmlkZWxpdmVyYWJsZS5jb20x
JDAiBgkqhkiG9w0BCQEWFWluZm9AaWRlbGl2ZXJhYmxlLmNvbTCCAiIwDQYJKoZI
hvcNAQEBBQADggIPADCCAgoCggIBAJ0LddkeiE9f0p60exUCR1PxrKBHoVwF5von
Lq+4GxA4I7Frtt8MV/dEYnYT44d/S6lEkAPLfF70CnyyWfpPO7MkIJYlFNZ2eJSz
9wu0VZ12CZO9RilxrEzK1xLS+f6g7P+rPP3m1CqSt84wpd3YloLdBuB3HwPWNh5H
a0Ew7W6IKyhzIX4MNzXXNc2evDIoaur3jQiPewtSastXzSQEEUx+S4St4qFbJ8lK
v6pwC1BmqX37uoNURqqS1CWg3iKiAfzqtTIwt9vBu8C1ev4ZnlKmAF4C1fTuH3Wo
X1lT+wpkycqWIUxoV7zoXYpxkleeNCw/GavDYmqhfU0qSjI4bdD/uy0s66dubtWB
rCZ3VRC7mjHhmEWj1Y9GYvvg6Fjt/BchVMNEhIBd873MdXDqIx0cV6SILiQgudtr
uvAUm5rT6KEYNSsHq9gQWN+LAgy8mDSOGLjYXdBtAOJMxS/w8XNA+bXWzvNYOl9q
QO8w7Py3U/8mnLkxgHaVGktWazwQ1QkwaLyQz7lbTBedXkxOzP3LvLAe+POd4Zre
wYcNUs/W51WfLup9o/IMEvDk/z0FCJ1fGFiHny3QFWrhJB3dAia1dwYtUpy42ZuY
w4UQEuZVr3BzDCXudx6638WOURpvaMe/Enfdr2bcqvCYvzbT06RPD/VTtuKtZ8O7
Q/hr0NvTAgMBAAGjgfMwgfAwEwYDVR0lBAwwCgYIKwYBBQUHAwEwgdgGA1UdAQSB
0DCBzYAQFBF2JL0Y1AduVxGNb4H7DqGBpjCBozELMAkGA1UEBhMCQ1kxETAPBgNV
BAgTCExpbWFzc29sMREwDwYDVQQHEwhMaW1hc3NvbDEZMBcGA1UEChMQSURlbGl2
ZXJhYmxlIEx0ZDETMBEGA1UECxMKT3BlcmF0aW9uczEYMBYGA1UEAxMPSURlbGl2
ZXJhYmxlIENBMSQwIgYJKoZIhvcNAQkBFhVpbmZvQGlkZWxpdmVyYWJsZS5jb22C
EMgq1WuLQwSJTj+ogMza3/YwDQYJKoZIhvcNAQENBQADggIBAEU9gdrydWT3ZwtA
LP/HEct2DJRQdK9WuL02x2xbdmszyb8xoayTIcJdvs2WHPBqxM417XAhNTZuG3Aw
Yx6OPSzbfmS1X7aVctSeBwZCfzxOk2MhTsKoZ+wjmFn6LVYz3P6K2deJVz6t+HRc
mMwFrsctzgGZsxIvMXa0eqyDE6hhxj0XKYLFmYNGBdUd5sW68Ko26Njv1FPLkoqo
Me2y+Gi6SltgjD/bmza36IRvaQVQtOjOxq+TncOBqLJ2+ikQOr93pkCgx3nso3CM
YH1ryRpXbSAb5aA9sF0jb6LLLUgHv5q2ExpEFcoRja5gww5R2VKwEJ2PXjK+2O/A
DgHOLKuiODxBF61+Ou/d29XiSX2HxCoH+onppi2h6ceJPcFwPrgzKVszsGkavtKL
7myxufgdBUgTFxYMaTEtrU0yaRiJItxreH1wj/vl1zKyl25MXZLM1LyFvszAX49h
9hgWApwE9hL1i6ygIfrpq2B+djPS8ZwX5S/t/QO5SmBjNLmw1GD1gHN5xqY0vMzS
VxDXrH6Kllhwipu8GLeMWn/VmWWLy1vr//GoBjujhplIZHJPYBrYjqwfxH1ZLa4B
O+N7bHDp63M0UVX5FdX1sN0laL29L1jXX/oe7x8PHNt3W4aOwVaIilh+/adXNDxr
1yumH4G7QEeMQisiMhlPxnZJHajX
-----END CERTIFICATE-----";

            var certBytes = Convert.FromBase64String(certBase64);
            var cert = new X509Certificate2(certBytes);

            return cert;
        }
    }
}