using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class DownloadController : ControllerBase
{
    private readonly FileHasher _fileHasher;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(FileHasher fileHasher, ILogger<DownloadController> logger)
    {
        _fileHasher = fileHasher;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Download([FromQuery] string FileName, [FromQuery] int partnumber)
    {
        _logger.LogInformation("[DownloadController] /Download endpoint hit. FileName={FileName}, partnumber={partnumber}", FileName, partnumber);
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), FileName);
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
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), FileName);
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
}
