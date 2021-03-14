using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramFileDownloader
{
    public class TelegramOptions
    {
        public string Token { get; set; }
        public List<int> AllowedSenderId { get; set; }
        public List<string> AllowedFileTypes { get; set; }
        public int? AllowedFileSize { get; set; }
    }
}
