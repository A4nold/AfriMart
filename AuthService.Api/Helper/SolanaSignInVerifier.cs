using System.Text;
using Chaos.NaCl;
using SimpleBase;

namespace AuthService.Api.Helper;

public static class SolanaSignInVerifier
{
    public static bool Verify(string message, string walletPubkeyBase58, string signature)
    {
        var msgBytes = Encoding.UTF8.GetBytes(message);
    
        var pubKey = Base58.Bitcoin.Decode(walletPubkeyBase58);
        if (pubKey.Length != 32) return false;

        byte[] sigBytes;
        try
        {
            sigBytes = Base58.Bitcoin.Decode(signature);
        }
        catch 
        {
            //fall back to base64
            try
            {
                sigBytes = Convert.FromBase64String(signature);
            }
            catch
            {
                return false;
            }
        }
        if (sigBytes.Length != 64) return false;

        return Ed25519.Verify(sigBytes, msgBytes, pubKey);
    }
}