using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using direct_file_transfer_client;
using Spectre.Console.Cli;
using Spectre.Console;

class Program
{
    static int Main(string[] args)
    {
        var app = new CommandApp<DownloadCommand>();
        return app.Run(args);
    }
}
