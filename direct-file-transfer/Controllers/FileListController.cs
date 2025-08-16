using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using direct_file_transfer;
using System.Collections.Generic;

namespace direct_file_transfer.Controllers
{
    public class FileListController : Controller
    {
        private readonly string _fileDirectory;
        public FileListController(AppConfig config)
        {
            _fileDirectory = config.FileDirectory ?? Directory.GetCurrentDirectory();
        }
        private string GetFileDirectory()
        {
            var config = (AppConfig)HttpContext?.RequestServices.GetService(typeof(AppConfig));
            return config?.FileDirectory ?? Directory.GetCurrentDirectory();
        }

        public IActionResult Index()
        {
            var fileDirectory = GetFileDirectory();
            if (!Directory.Exists(fileDirectory))
                return View(new List<string>());
            var files = Directory.GetFiles(fileDirectory).Select(Path.GetFileName).ToList();
            return View(files);
        }
    }
}
