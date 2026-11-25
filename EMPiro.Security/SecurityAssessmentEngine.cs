using Empiro.Core;
using Empiro.Domain;
namespace Empiro.Security;
public sealed class SecurityAssessmentEngine : ISecurityCheck
{
    public IEnumerable<Finding> Assess(Asset asset)
    {
        foreach (var service in asset.Services)
        {
            if (service.Port is 21 or 23) yield return new(asset.Ip, "Warning", "Potential cleartext management service", $"TCP/{service.Port} is open; {service.Evidence}", "Verify the service and prefer encrypted management protocols.");
            if (service.Port == 3389) yield return new(asset.Ip, "Review", "Remote desktop exposed", "TCP/3389 accepts connections on this scan path.", "Restrict access to trusted management networks.");
            if (service.Name == "HTTP" && service.Confidence >= 90) yield return new(asset.Ip, "Review", "Unencrypted HTTP available", "HTTP protocol response observed.", "Use HTTPS for sensitive administration where supported.");
        }
    }
}
