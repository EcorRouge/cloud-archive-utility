using System.Collections.Generic;

namespace EcorRouge.Archive.Utility.CloudConnectors;

public interface IConnectionFactory
{
    ICloudConnectionCredentials TranslateCredentials(Dictionary<string, string> connectionProperties);
    ICloudConnection Connect(dynamic cloudCredentials);
}