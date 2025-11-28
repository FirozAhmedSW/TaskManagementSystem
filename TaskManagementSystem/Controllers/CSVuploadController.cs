using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using TaskManagementSystem.DataContext;
using TaskManagementSystem.Models;
using TaskManagementSystem.Services;

namespace TaskManagementSystem.Controllers
{
    public class CSVuploadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ActivityLogger _logger;
        private readonly IWebHostEnvironment _environment;

        public CSVuploadController(ApplicationDbContext context, ActivityLogger logger, IWebHostEnvironment environment)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction("Index");
            }

            var uploadedUsers = ReadUsersFromExcel(file);

            if (uploadedUsers.Count == 0)
            {
                TempData["Error"] = "No valid users found in the file.";
                return RedirectToAction("Index");
            }

            var existingUsers = await _context.Users.ToListAsync();

            var matched = new List<User>();
            var nonMatched = new List<User>();

            foreach (var user in uploadedUsers)
            {
                if (existingUsers.Any(x => x.UserName == user.UserName || x.Email == user.Email))
                    matched.Add(user);
                else
                    nonMatched.Add(user);
            }

            var viewModel = new CSVUser
            {
                MatchedUsers = matched,
                NonMatchedUsers = nonMatched
            };

            // Store in TempData for the import action
            TempData["UploadedUsers"] = System.Text.Json.JsonSerializer.Serialize(uploadedUsers);

            return View("Index", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ImportUsers()
        {
            try
            {
                var usersJson = TempData["UploadedUsers"]?.ToString();
                if (string.IsNullOrEmpty(usersJson))
                {
                    TempData["Error"] = "No users to import. Please upload a file first.";
                    return RedirectToAction("Index");
                }

                var uploadedUsers = System.Text.Json.JsonSerializer.Deserialize<List<User>>(usersJson);
                var existingUsers = await _context.Users.ToListAsync();

                int addedCount = 0;
                int updatedCount = 0;
                int skippedCount = 0;

                foreach (var user in uploadedUsers)
                {
                    // Check if user exists by UserName or Email
                    var existingUser = existingUsers.FirstOrDefault(x =>
                        x.UserName == user.UserName || x.Email == user.Email);

                    if (existingUser != null)
                    {
                        // Update existing user
                        existingUser.Email = user.Email ?? existingUser.Email;
                        existingUser.Password = user.Password ?? existingUser.Password;
                        existingUser.Address = user.Address ?? existingUser.Address;
                        existingUser.Contact = user.Contact ?? existingUser.Contact;
                        existingUser.About = user.About ?? existingUser.About;
                        existingUser.PhotoPath = user.PhotoPath ?? existingUser.PhotoPath;
                        existingUser.RoleId = user.RoleId ?? existingUser.RoleId;
                        existingUser.UpdatedBy = user.UpdatedBy;
                        existingUser.UpdatedAt = DateTime.Now;

                        _context.Users.Update(existingUser);
                        updatedCount++;
                    }
                    else
                    {
                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(user.UserName) || string.IsNullOrWhiteSpace(user.Email))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Add new user
                        user.CreatedAt = DateTime.Now;
                        user.IsActive = true;
                        _context.Users.Add(user);
                        addedCount++;
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Import completed! Added: {addedCount}, Updated: {updatedCount}, Skipped: {skippedCount}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error importing users: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        public FileResult DownloadTemplate()
        {
            var csv =
                "UserName,Email,Password,Address,Contact,About,CreatedBy,UpdatedBy,PhotoPath,RoleId\r\n" +
                "john_doe,john@example.com,123456,New York,01234567890,Sample user 1,Admin,,photos/john.jpg,1\r\n" +
                "mary_smith,mary@example.com,pass789,Los Angeles,09876543210,Sample user 2,Admin,,photos/mary.jpg,2\r\n";

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

            return File(bytes, "text/csv", "users-template.csv");
        }

        public List<User> ReadUsersFromExcel(IFormFile file)
        {
            var users = new List<User>();

            try
            {
                if (file == null || file.Length == 0)
                {
                    return users;
                }

                var fileExtension = Path.GetExtension(file.FileName).ToLower();

                // Check if it's a CSV file
                if (fileExtension == ".csv")
                {
                    users = ReadUsersFromCSV(file);
                }
                else if (fileExtension == ".xlsx" || fileExtension == ".xls")
                {
                    users = ReadUsersFromExcelFile(file);
                }
                else
                {
                    throw new Exception("Unsupported file format. Please upload .csv, .xlsx, or .xls file.");
                }
            }
            catch (Exception ex)
            {
                // Log error
                throw;
            }

            return users;
        }

        private List<User> ReadUsersFromCSV(IFormFile file)
        {
            var users = new List<User>();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                // Read header line
                var headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine))
                    return users;

                var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();

                // Read data lines
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var values = line.Split(',');

                        // Skip if all values are empty
                        if (values.All(string.IsNullOrWhiteSpace))
                            continue;

                        var user = new User();

                        for (int i = 0; i < headers.Length && i < values.Length; i++)
                        {
                            var header = headers[i];
                            var value = values[i]?.Trim();

                            switch (header)
                            {
                                case "UserName":
                                    user.UserName = value;
                                    break;
                                case "Email":
                                    user.Email = value;
                                    break;
                                case "Password":
                                    user.Password = value;
                                    break;
                                case "Address":
                                    user.Address = value;
                                    break;
                                case "Contact":
                                    user.Contact = value;
                                    break;
                                case "About":
                                    user.About = value;
                                    break;
                                case "PhotoPath":
                                    user.PhotoPath = value;
                                    break;
                                case "CreatedBy":
                                    user.CreatedBy = value;
                                    break;
                                case "UpdatedBy":
                                    user.UpdatedBy = value;
                                    break;
                                case "RoleId":
                                    if (int.TryParse(value, out int roleId))
                                        user.RoleId = roleId;
                                    break;
                            }
                        }

                        user.IsActive = true;
                        users.Add(user);
                    }
                    catch (Exception)
                    {
                        // Skip problematic rows
                        continue;
                    }
                }
            }

            return users;
        }

        private List<User> ReadUsersFromExcelFile(IFormFile file)
        {
            var users = new List<User>();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using (var stream = file.OpenReadStream())
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                });

                var table = result.Tables[0];

                foreach (DataRow row in table.Rows)
                {
                    try
                    {
                        // Skip empty rows
                        if (row.ItemArray.All(field => field == null || string.IsNullOrWhiteSpace(field.ToString())))
                            continue;

                        users.Add(new User
                        {
                            UserName = table.Columns.Contains("UserName") ? row["UserName"]?.ToString()?.Trim() : "",
                            Email = table.Columns.Contains("Email") ? row["Email"]?.ToString()?.Trim() : "",
                            Password = table.Columns.Contains("Password") ? row["Password"]?.ToString()?.Trim() : "",
                            Address = table.Columns.Contains("Address") ? row["Address"]?.ToString()?.Trim() : "",
                            Contact = table.Columns.Contains("Contact") ? row["Contact"]?.ToString()?.Trim() : "",
                            About = table.Columns.Contains("About") ? row["About"]?.ToString()?.Trim() : "",
                            PhotoPath = table.Columns.Contains("PhotoPath") ? row["PhotoPath"]?.ToString()?.Trim() : "",
                            CreatedBy = table.Columns.Contains("CreatedBy") ? row["CreatedBy"]?.ToString()?.Trim() : "",
                            UpdatedBy = table.Columns.Contains("UpdatedBy") ? row["UpdatedBy"]?.ToString()?.Trim() : "",
                            RoleId = table.Columns.Contains("RoleId") && int.TryParse(row["RoleId"]?.ToString(), out int roleVal) ? roleVal : (int?)null,
                            IsActive = true
                        });
                    }
                    catch (Exception)
                    {
                        // Skip problematic rows
                        continue;
                    }
                }
            }

            return users;
        }
    }
}