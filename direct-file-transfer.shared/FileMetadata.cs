using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace direct_file_transfer.shared
{
    public class FileMetadata
    {
        public string Name { get; set; }
        public IEnumerable<FileVersionMetadata> MyProperty { get; set; }
    }

    public class FileVersionMetadata
    {
        public long Size { get; set; }
        public int PartCount { get; set; }
    }
}
