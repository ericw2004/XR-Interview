using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

public class AcceptAllCertificates : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true; // Accept all certificates
    }
    
}
