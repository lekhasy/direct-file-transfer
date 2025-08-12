using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class DownloadController : ControllerBase
{
    private readonly FileHasher _fileHasher;
    private readonly ILogger<DownloadController> _logger;
    private readonly string _fileDirectory;
    private readonly FileIndexCache _fileIndexCache;
    private readonly AppConfig _config;

        public DownloadController(FileHasher fileHasher, ILogger<DownloadController> logger, Microsoft.Extensions.Options.IOptions<AppConfig> configOptions, FileIndexCache fileIndexCache)
        {
            _fileHasher = fileHasher;
            _logger = logger;
            _config = configOptions.Value;
            _fileDirectory = _config.FileDirectory ?? Directory.GetCurrentDirectory();
            _fileIndexCache = fileIndexCache;
        }

    [HttpGet]
    public IActionResult Download([FromQuery] string FileName, [FromQuery] int partnumber)
    {
        _logger.LogInformation("[DownloadController] /Download endpoint hit. FileName={FileName}, partnumber={partnumber}", FileName, partnumber);
        // Prevent directory traversal
        if (FileName.Contains("..") || Path.IsPathRooted(FileName))
        {
            _logger.LogWarning("Directory traversal attempt detected: {FileName}", FileName);
            return BadRequest("Invalid file name.");
        }
        var filePath = Path.Combine(_fileDirectory, FileName);
        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FileName}", FileName);
            return NotFound($"File {FileName} not found.");
        }

        List<string> hashTable;
        try
        {
            _logger.LogInformation("Calculating or retrieving hash table for file: {FileName}", FileName);
            hashTable = _fileHasher.GetOrCalculateHashTable(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating hash table for file: {FileName}", FileName);
            return Problem($"Error calculating hash table: {ex.Message}");
        }

        if (partnumber < 0 || partnumber >= hashTable.Count)
        {
            _logger.LogWarning("Invalid part number {partnumber} for file {FileName}", partnumber, FileName);
            return BadRequest($"Invalid part number. Valid range: 0 to {hashTable.Count - 1}");
        }

        _logger.LogInformation("Reading part {partnumber} of file {FileName}", partnumber, FileName);
        var partData = _fileHasher.ReadFilePart(filePath, partnumber);
        var partHash = hashTable[partnumber];

        _logger.LogInformation("Returning part {partnumber} of file {FileName} with hash {partHash}", partnumber, FileName, partHash);
        return Ok(new
        {
            FileName,
            partnumber,
            partHash,
            partData = Convert.ToBase64String(partData)
        });
    }

    [HttpGet("metadata")]
    public IActionResult GetFileMetadata([FromQuery] string FileName)
    {
        _logger.LogInformation("[DownloadController] /Download/metadata endpoint hit. FileName={FileName}", FileName);
        // Prevent directory traversal
        if (FileName.Contains("..") || Path.IsPathRooted(FileName))
        {
            _logger.LogWarning("Directory traversal attempt detected: {FileName}", FileName);
            return BadRequest("Invalid file name.");
        }
        var filePath = Path.Combine(_fileDirectory, FileName);
        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FileName}", FileName);
            return NotFound($"File {FileName} not found.");
        }

        List<string> hashTable;
        try
        {
            _logger.LogInformation("Calculating or retrieving hash table for file: {FileName}", FileName);
            hashTable = _fileHasher.GetOrCalculateHashTable(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating hash table for file: {FileName}", FileName);
            return Problem($"Error calculating hash table: {ex.Message}");
        }

        var fileInfo = new System.IO.FileInfo(filePath);
        _logger.LogInformation("Returning metadata for file {FileName}: Size={FileSize}, Parts={PartCount}", FileName, fileInfo.Length, hashTable.Count);
        return Ok(new
        {
            FileName,
            FileSize = fileInfo.Length,
            PartCount = hashTable.Count,
            PartHashes = hashTable,
            ChunkSize = 4 * 1024 * 1024 // 4MB
        });
    }

    [HttpGet("index")]
    public IActionResult GetFileIndex()
    {
        _logger.LogInformation("[DownloadController] /Download/index endpoint hit.");
        if (!Directory.Exists(_fileDirectory))
        {
            _logger.LogWarning("Configured file directory does not exist: {FileDirectory}", _fileDirectory);
            return NotFound("Configured file directory does not exist.");
        }

        var result = _fileIndexCache.GetIndex();
        return Ok(result);
    }
}
