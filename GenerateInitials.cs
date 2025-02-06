using Microsoft.Data.SqlClient; 
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class GenerateInitials
{
    private readonly ILogger _logger;

    public GenerateInitials(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GenerateInitials>();
    }

   [Function("GenerateInitials")] //this handles our communication with client/postman/RPA makes sure that correct  responses are send - need to have a look at parsing logs from function further down
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Processing GenerateInitials request.");

            // Read the request body from input 
            var requestBody = await req.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
            string employeeName = data?.EmployeeName ?? "";

            // Makes sure that an employee name is sypplied
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("EmployeeName is required.");
                return errorResponse;
            }

            try
            {
                // Generate and store initials - also connect to sql
                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                string initials = await GenerateAndStoreInitialsAsync(employeeName, connectionString);

                // Prepare success response
                var response = new { EmployeeInitials = initials };
                var httpResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(response);

                return httpResponse;
            }
            catch (InvalidOperationException ex)
            {
                // Prepare error response for no available initials combinations
                _logger.LogError(ex, "Failed to generate unique initials.");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
                await errorResponse.WriteStringAsync("Unable to generate unique initials after multiple attempts.");
                return errorResponse;
            }
            catch (TimeoutException ex)
            {
                // Prepare error response for too many retries
                _logger.LogError(ex, "SQL server not spun up yet, retry.");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.RequestTimeout);
                await errorResponse.WriteStringAsync("SQL server timed out, try again");
                return errorResponse;
            }
        }


    private async Task<string> GenerateAndStoreInitialsAsync(string name, string connectionString) // our main guy, handles connecting to sql, inserting and  timeouts - should be renamed!
{
    
    {
        const int maxRetries = 9; // This is the number of strategies defined in the switch - this could be improved!
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
              try
        {
            _logger.LogInformation($"Opening connection to SQL server. Attempt {retryCount + 1}/{maxRetries}.");

            // Create and open a new connection inside the loop
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                _logger.LogInformation("SQL connection opened successfully.");
                // OBS OBS OBS OBS Currently retries are used if sql server is unresponsive - should divide this in next iteration.

                // Generate initials based on the retry strategy - calls function with strategies
                string initials = GenerateInitialsByStrategy(name, retryCount);

                // Attempt to insert the initials into the database - the business logic of no similar initials permitted are handled in the catch section.
                string query = "INSERT INTO EmployeeInitials (Initials) VALUES (@Initials)";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Initials", initials);
                    await command.ExecuteNonQueryAsync();
                    return initials; // Success, return the initials
                }
            }
        }
            catch (SqlException ex) when (ex.Number == 2627) // Unique constraint violation - this makes sure that only unique initials can be generated
            {
                // Increment retry count and try the next strategy
                retryCount++;
            }
            catch (SqlException ex) when (ex.Number == -2 || ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase)) // Timeout from Azure SQL server - again please note this uses up retries
            {
                if (retryCount < maxRetries)
                {
                    retryCount++;
                    _logger.LogWarning($"SQL timeout occurred. Retrying... Attempt {retryCount}/{maxRetries}");
                    await Task.Delay(8000); // Add delay
                }
                else
                {
                    throw new TimeoutException("SQL server timed out after multiple attempts. Please try again later.", ex);
                }
            }
        }   

        // If all retries fail, throw an exception
        throw new InvalidOperationException($"Unable to generate unique initials for '{name}' after {maxRetries} attempts.");
    }
}

private string GenerateInitialsByStrategy(string name, int retryCount)
{
    // Split the name into parts
    var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries); //split by space
    string firstName = parts.Length > 0 ? parts[0] : string.Empty; // if the number of parts is more than 0 return first part, if it is 0 return empty string. Can seem silly, but names can be wierd mmkay
    string middleName = parts.Length > 2 ? parts[1] : string.Empty;
    string lastName = parts.Length > 1 ? parts[^1] : string.Empty;

    return retryCount switch // this switch uses the getletters function to fetch desired chars from the input name, it will continue to retry untill all possibilities are exausted.
    //This is done according to Business logic from BLAA in HR
    {
        0 => (GetLetters(firstName, 2) + GetLetters(lastName, 3)).ToUpper(), // 2 first letters from First + 3 first letters from Last
        1 => (GetLetters(firstName, 3) + GetLetters(lastName, 2)).ToUpper(), // 3 first letters from First + 2 first letters from Last
        2 => (GetLetters(firstName, 4) + GetLetters(lastName, 1)).ToUpper(), // 4 first letters from First + 1 first letter from Last
        3 => (GetLetters(firstName, 1) + GetLetters(lastName, 4)).ToUpper(), // 1 first letter from First + 4 first letters from Last
        4 => (GetLetters(firstName, 5) + GetLetters(lastName, 0)).ToUpper(), // 5 first letters from First + 0 letters from Last
        5 => (GetLetters(firstName, 0) + GetLetters(lastName, 5)).ToUpper(), // 0 letters from First + 5 first letters from Last
        6 when !string.IsNullOrEmpty(middleName) => (GetLetters(firstName, 2) + GetLetters(middleName, 1) + GetLetters(lastName, 2)).ToUpper(), // 2 first from First + 1 from Middle + 2 from Last
        7 when !string.IsNullOrEmpty(middleName) => (GetLetters(firstName, 1) + GetLetters(middleName, 1) + GetLetters(lastName, 3)).ToUpper(), // 1 first from First + 1 from Middle + 3 from Last
        8 when !string.IsNullOrEmpty(middleName) => (GetLetters(firstName, 2) + GetLetters(middleName, 2) + GetLetters(lastName, 1)).ToUpper(), // 2 first from First + 2 from Middle + 1 from Last
        _ => throw new InvalidOperationException("No valid strategy found."),
    };
}

private string GetLetters(string part, int count)
{
    // this makes sure that there is a name we can substring, and if there is, we will take from position 0 to pos X letters and output them - should look in to randomiser function. also æøå
    return part.Length >= count ? part.Substring(0, count) : part;
}

}
