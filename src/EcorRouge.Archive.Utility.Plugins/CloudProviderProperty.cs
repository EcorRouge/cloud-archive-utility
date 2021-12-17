using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.Plugins
{
    public class CloudProviderProperty
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public CloudProviderPropertyType PropertyType { get; set; }
    }

    public enum CloudProviderPropertyType
    {
        String,
        Integer,
        Double,
        Password,
        FolderPath
    }
}
