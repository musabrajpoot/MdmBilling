
using Microsoft.AspNetCore.DataProtection;

namespace MdmBilling.Services;

public class CredentialProtector
{
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(
            "MdmBilling.ServerCredentials.v1"
        );
    }

    public string Protect(string plainText)
    {
        return _protector.Protect(plainText);
    }

    public string Unprotect(string protectedText)
    {
        return _protector.Unprotect(protectedText);
    }
}
