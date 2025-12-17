using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace SchedulerPlatform.Tests.ApiParsing;

/// <summary>
/// Unit tests for ADR API response parsing.
/// The ADR API can return responses in multiple formats:
/// - JSON object with StatusId, Status, IndexId fields
/// - JSON array containing response objects
/// - Plain 64-bit integer (IndexId only)
/// - Error responses with validation messages
/// </summary>
public class AdrApiResponseParsingTests
{
    #region Success Response Parsing Tests

    [Fact]
    public void ParseResponse_JsonObject_ExtractsAllFields()
    {
        // Arrange - Standard JSON object response
        var jsonResponse = """
            {
                "JobId": 12345,
                "StatusId": 11,
                "Status": "Complete"
            }
            """;

        // Act
        var result = ParseAdrStatusResponse(jsonResponse);

        // Assert
        result.Should().NotBeNull();
        result!.StatusId.Should().Be(11);
        result.StatusDescription.Should().Be("Complete");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ParseResponse_JsonArray_UsesFirstElement()
    {
        // Arrange - Array response (sometimes returned by API)
        var jsonResponse = """
            [
                {
                    "JobId": 12345,
                    "StatusId": 6,
                    "Status": "Sent To AI"
                }
            ]
            """;

        // Act
        var result = ParseAdrStatusResponseArray(jsonResponse);

        // Assert
        result.Should().NotBeNull();
        result!.StatusId.Should().Be(6);
        result.StatusDescription.Should().Be("Sent To AI");
    }

    [Fact]
    public void ParseResponse_PlainInteger_ParsesAsIndexId()
    {
        // Arrange - Plain integer response (IndexId only)
        var response = "1234567890";

        // Act
        var result = ParsePlainIntegerResponse(response);

        // Assert
        result.Should().NotBeNull();
        result!.IndexId.Should().Be(1234567890);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ParseResponse_PlainInteger_HandlesLargeValues()
    {
        // Arrange - Large 64-bit integer
        var response = "9223372036854775807"; // long.MaxValue

        // Act
        var result = ParsePlainIntegerResponse(response);

        // Assert
        result.Should().NotBeNull();
        result!.IndexId.Should().Be(long.MaxValue);
    }

    [Fact]
    public void ParseResponse_EmptyResponse_ReturnsSuccessWithNoContent()
    {
        // Arrange
        var response = "";

        // Act
        var result = ParseEmptyResponse(response);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.StatusDescription.Should().Contain("no content");
    }

    [Fact]
    public void ParseResponse_WhitespaceResponse_TreatedAsEmpty()
    {
        // Arrange
        var response = "   \n\t  ";

        // Act
        var result = ParseEmptyResponse(response);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Status Code Parsing Tests

    [Theory]
    [InlineData(1, "Inserted", false)]
    [InlineData(2, "Inserted With Priority", false)]
    [InlineData(3, "Invalid CredentialID", true)]
    [InlineData(4, "Cannot Connect To VCM", true)]
    [InlineData(5, "Cannot Insert Into Queue", true)]
    [InlineData(6, "Sent To AI", false)]
    [InlineData(7, "Cannot Connect To AI", true)]
    [InlineData(8, "Cannot Save Result", true)]
    [InlineData(9, "Needs Human Review", true)]
    [InlineData(10, "Received From AI", false)]
    [InlineData(11, "Complete", true)]
    [InlineData(12, "Login Attempt Succeeded", false)]
    [InlineData(13, "No Documents Found", false)]
    [InlineData(14, "Failed To Process All Documents", true)]
    [InlineData(15, "No Documents Processed", false)]
    public void ParseResponse_AllStatusCodes_ParsedCorrectly(int statusId, string expectedStatus, bool isFinal)
    {
        // Arrange
        var jsonResponse = $$"""
            {
                "JobId": 12345,
                "StatusId": {{statusId}},
                "Status": "{{expectedStatus}}"
            }
            """;

        // Act
        var result = ParseAdrStatusResponse(jsonResponse);

        // Assert
        result.Should().NotBeNull();
        result!.StatusId.Should().Be(statusId);
        result.StatusDescription.Should().Be(expectedStatus);
        
        // Verify finality determination
        var actualIsFinal = IsFinalStatus(statusId);
        actualIsFinal.Should().Be(isFinal, $"StatusId {statusId} ({expectedStatus}) should {(isFinal ? "be" : "not be")} final");
    }

    #endregion

    #region Error Response Parsing Tests

    [Fact]
    public void ParseResponse_4xxWithIndexId_ExtractsIndexIdFromError()
    {
        // Arrange - Partial success: record created but credential verification failed
        var jsonResponse = """
            {
                "error": "Credential verification failed",
                "IndexId": 987654321
            }
            """;

        // Act
        var result = ParseErrorResponseWithIndexId(jsonResponse);

        // Assert
        result.Should().NotBeNull();
        result!.IndexId.Should().Be(987654321);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void ParseResponse_ValidationErrors_ReturnsErrorList()
    {
        // Arrange - Validation failure response
        var jsonResponse = """
            [
                "SourceApplicationName must not be null or empty",
                "RecipientEmail must not be null or empty"
            ]
            """;

        // Act
        var errors = ParseValidationErrors(jsonResponse);

        // Assert
        errors.Should().HaveCount(2);
        errors.Should().Contain("SourceApplicationName must not be null or empty");
        errors.Should().Contain("RecipientEmail must not be null or empty");
    }

    [Fact]
    public void ParseResponse_InvalidJson_HandlesGracefully()
    {
        // Arrange - Malformed JSON
        var response = "{ invalid json }";

        // Act
        var result = TryParseResponse(response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Error deserializing");
    }

    [Fact]
    public void ParseResponse_UnexpectedFormat_ReturnsError()
    {
        // Arrange - Unexpected string response
        var response = "Unexpected server error occurred";

        // Act
        var result = TryParseResponse(response);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.ErrorMessage.Should().Contain("unexpected");
    }

    #endregion

    #region Case Insensitivity Tests

    [Fact]
    public void ParseResponse_CaseInsensitive_ParsesLowercaseFields()
    {
        // Arrange - Lowercase field names
        var jsonResponse = """
            {
                "jobid": 12345,
                "statusid": 11,
                "status": "Complete"
            }
            """;

        // Act
        var result = ParseAdrStatusResponse(jsonResponse);

        // Assert
        result.Should().NotBeNull();
        result!.StatusId.Should().Be(11);
    }

    [Fact]
    public void ParseResponse_CaseInsensitive_ParsesMixedCaseFields()
    {
        // Arrange - Mixed case field names
        var jsonResponse = """
            {
                "JobID": 12345,
                "STATUSID": 11,
                "Status": "Complete"
            }
            """;

        // Act
        var result = ParseAdrStatusResponse(jsonResponse);

        // Assert
        result.Should().NotBeNull();
        result!.StatusId.Should().Be(11);
    }

    #endregion

    #region Helper Classes and Methods

    private class AdrApiResult
    {
        public int? StatusId { get; set; }
        public string? StatusDescription { get; set; }
        public long? IndexId { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsError { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private class AdrApiResponse
    {
        public int JobId { get; set; }
        public int StatusId { get; set; }
        public string? Status { get; set; }
        public long? IndexId { get; set; }
    }

    private static AdrApiResult? ParseAdrStatusResponse(string jsonResponse)
    {
        try
        {
            var response = JsonSerializer.Deserialize<AdrApiResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (response == null) return null;

            return new AdrApiResult
            {
                StatusId = response.StatusId,
                StatusDescription = response.Status,
                IndexId = response.IndexId,
                IsSuccess = true,
                IsError = false
            };
        }
        catch
        {
            return null;
        }
    }

    private static AdrApiResult? ParseAdrStatusResponseArray(string jsonResponse)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<AdrApiResponse>>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var response = list?.FirstOrDefault();
            if (response == null) return null;

            return new AdrApiResult
            {
                StatusId = response.StatusId,
                StatusDescription = response.Status,
                IndexId = response.IndexId,
                IsSuccess = true,
                IsError = false
            };
        }
        catch
        {
            return null;
        }
    }

    private static AdrApiResult? ParsePlainIntegerResponse(string response)
    {
        var trimmed = response.Trim();
        if (long.TryParse(trimmed, out var indexId))
        {
            return new AdrApiResult
            {
                IndexId = indexId,
                IsSuccess = true,
                IsError = false
            };
        }
        return null;
    }

    private static AdrApiResult ParseEmptyResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new AdrApiResult
            {
                IsSuccess = true,
                IsError = false,
                StatusDescription = "ADR API returned no content."
            };
        }
        return new AdrApiResult { IsSuccess = false, IsError = true };
    }

    private static AdrApiResult? ParseErrorResponseWithIndexId(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var result = new AdrApiResult { IsError = true, IsSuccess = false };

            if (doc.RootElement.TryGetProperty("IndexId", out var indexIdProp) ||
                doc.RootElement.TryGetProperty("indexId", out indexIdProp))
            {
                if (indexIdProp.TryGetInt64(out var indexId))
                {
                    result.IndexId = indexId;
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ParseValidationErrors(string jsonResponse)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(jsonResponse) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static AdrApiResult TryParseResponse(string response)
    {
        var trimmed = response.TrimStart();

        // Try JSON object
        if (trimmed.StartsWith("{"))
        {
            try
            {
                var result = ParseAdrStatusResponse(response);
                if (result != null) return result;
            }
            catch { }
        }

        // Try JSON array
        if (trimmed.StartsWith("["))
        {
            try
            {
                var result = ParseAdrStatusResponseArray(response);
                if (result != null) return result;
            }
            catch { }
        }

        // Try plain integer
        if (long.TryParse(trimmed, out _))
        {
            var result = ParsePlainIntegerResponse(response);
            if (result != null) return result;
        }

        // Unknown format
        return new AdrApiResult
        {
            IsSuccess = false,
            IsError = true,
            ErrorMessage = $"Error deserializing or unexpected content: {response}"
        };
    }

    /// <summary>
    /// Determines if a status is final (no more processing needed).
    /// Mirrors the logic in AdrOrchestratorService.IsFinalStatus
    /// </summary>
    private static bool IsFinalStatus(int statusId)
    {
        return statusId switch
        {
            11 => true,  // Complete (Document Retrieval Complete)
            9 => true,   // Needs Human Review
            3 => true,   // Invalid CredentialID (error - final)
            4 => true,   // Cannot Connect To VCM (error - final)
            5 => true,   // Cannot Insert Into Queue (error - final)
            7 => true,   // Cannot Connect To AI (error - final)
            8 => true,   // Cannot Save Result (error - final)
            14 => true,  // Failed To Process All Documents (error - final)
            _ => false   // 1, 2, 6, 10, 12, 13, 15 - still processing or retry
        };
    }

    #endregion
}
