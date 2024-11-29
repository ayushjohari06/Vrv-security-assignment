using CrudApi.Models;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Security.Claims;
using System.Text;

namespace CrudApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserContext _userContext;
        private IConverter _pdfConverter;

        public UserController(UserContext userContext, IConverter pdfConverter)
        {
            _userContext = userContext;
            _pdfConverter = pdfConverter;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userContext.Users.ToListAsync();
            return Ok(users);
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetSingleUser(string userId)
        {
            if (!Guid.TryParse(userId, out _))
            {
                return BadRequest("Invalid userId format");
            }

            var user = await _userContext.Users.FindAsync(userId); // Fetch a single user from the database

            if (user == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            return Ok(user);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser(User newUser)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            newUser.Id = Guid.NewGuid().ToString();

            _userContext.Users.Add(newUser);
            await _userContext.SaveChangesAsync(); // Save changes to the database

            return Created("", newUser);
        }

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] User updatedUser)
        {
            // Validate userId
            if (!Guid.TryParse(userId, out _))
            {
                return BadRequest("Invalid userId format");
            }

            // Fetch the existing user from the database
            var existingUser = await _userContext.Users.FindAsync(userId);

            if (existingUser == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            // Check if the logged-in user is an admin or the owner of the user data
            var loggedInUserId = User.FindFirst(ClaimTypes.Name)?.Value;

            if (!User.IsInRole("Admin") && loggedInUserId != userId)
            {
                return Forbid(); // Non-admin users can't update other users' data
            }

            // Update user data
            existingUser.Username = updatedUser.Username;
            existingUser.Password = updatedUser.Password;
            existingUser.IsAdmin = updatedUser.IsAdmin;
            existingUser.Age = updatedUser.Age;
            existingUser.Hobbies = updatedUser.Hobbies;

            // Save changes to the database
            await _userContext.SaveChangesAsync();

            return Ok(existingUser);
        }

        [HttpDelete("{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            // Validate userId
            if (!Guid.TryParse(userId, out _))
            {
                return BadRequest("Invalid userId format");
            }

            var userToDelete = await _userContext.Users.FindAsync(userId);

            if (userToDelete == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            _userContext.Users.Remove(userToDelete);
            await _userContext.SaveChangesAsync();

            return NoContent(); // 204 if the record is found and deleted
        }

        [HttpPost("search")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SearchUsers([FromBody] Dictionary<string, string> filters)
        {
            try
            {
                var query = _userContext.Users.AsQueryable();

                foreach (var filter in filters)
                {
                    switch (filter.Key.ToLower())
                    {
                        case "username":
                            query = query.Where(u => u.Username.ToUpper().Contains(filter.Value.ToUpper()));
                            break;
                        case "age":
                            if (int.TryParse(filter.Value, out int age))
                            {
                                query = query.Where(u => u.Age == age);
                            }
                            break;
                            // Add more cases for other properties as needed
                    }
                }

                var result = await query.ToListAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        [HttpPost("export")]
        [Authorize(Roles = "Admin")]
        public IActionResult ExportUsers([FromBody] ExportRequest request)
        {
            try
            {
                var usersToExport = _userContext.Users.AsQueryable();

                foreach (var filter in request.Filters)
                {
                    switch (filter.Key.ToLower())
                    {
                        case "username":
                            usersToExport = usersToExport.Where(u => u.Username.ToUpper().Contains(filter.Value.ToUpper()));
                            break;
                        case "age":
                            if (int.TryParse(filter.Value, out int age))
                            {
                                usersToExport = usersToExport.Where(u => u.Age == age);
                            }
                            break;
                            // Add more cases for other properties as needed
                    }
                }

                var exportData = usersToExport.ToList(); // Convert IQueryable to a list or collection for export

                MemoryStream stream;
                string contentType;

                switch (request.Format?.ToLower())
                {
                    case "pdf":
                        stream = ExportToPdf(exportData);
                        contentType = "application/pdf";
                        break;
                    case "excel":
                        stream = ExportToExcel(exportData);
                        contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        break;
                    default:
                        return BadRequest("Invalid export format. Supported formats: PDF, Excel.");
                }

                // Add Header: Current Date
                Response.Headers.Add("Current-Date", DateTime.UtcNow.ToString("yyyy-MM-dd"));

                // Add Footer: page number
                Response.Headers.Add("Page-Number", "1");

                // Return the exported file
                return File(stream.ToArray(), contentType, $"users_export.{request.Format}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        private MemoryStream ExportToPdf(List<User> data)
        {
            var htmlContent = GenerateHtmlContent(data);

            var globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 }
            };

            var objectSettings = new ObjectSettings
            {
                PagesCount = true,
                HtmlContent = htmlContent,
                WebSettings = { DefaultEncoding = "utf-8", UserStyleSheet = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "styles", "styles.css") },
                HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true },
                FooterSettings = { FontSize = 9, Line = true, Right = "[page]" }
            };

            var pdf = new HtmlToPdfDocument()
            {
                GlobalSettings = globalSettings,
                Objects = { objectSettings }
            };

            return new MemoryStream(_pdfConverter.Convert(pdf));
        }

        private string GenerateHtmlContent(List<User> data)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("<html><head><link rel='stylesheet' type='text/css' href='styles.css'></head><body>");
            stringBuilder.Append("<table border='1' cellpadding='5' cellspacing='0'>");
            stringBuilder.Append("<tr><th>ID</th><th>Username</th><th>Age</th></tr>");

            foreach (var user in data)
            {
                stringBuilder.Append($"<tr><td>{user.Id}</td><td>{user.Username}</td><td>{user.Age}</td></tr>");
            }

            stringBuilder.Append("</table></body></html>");

            return stringBuilder.ToString();
        }

        private MemoryStream ExportToExcel(List<User> data)
        {
            try
            {
                // Set LicenseContext for ExcelPackage
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Users");

                    // Add headers
                    worksheet.Cells["A1"].Value = "ID";
                    worksheet.Cells["B1"].Value = "Username";
                    worksheet.Cells["C1"].Value = "Age";

                    // Populate data
                    for (var i = 0; i < data.Count; i++)
                    {
                        var user = data[i];
                        var row = i + 2;

                        worksheet.Cells[$"A{row}"].Value = user.Id;
                        worksheet.Cells[$"B{row}"].Value = user.Username;
                        worksheet.Cells[$"C{row}"].Value = user.Age;
                    }

                    // Auto-fit columns
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    // Save to MemoryStream
                    var stream = new MemoryStream(package.GetAsByteArray());
                    return stream;
                }

            }
            catch (Exception ex)
            {
                return null;
            }


            
        }

    }
}
