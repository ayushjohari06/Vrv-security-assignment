using System.ComponentModel.DataAnnotations;

namespace CrudApi.Models
{
    public class User
    {
        public string? Id { get; set; }

        [Required(ErrorMessage ="Username is required")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }

        public bool IsAdmin { get; set; } = false;

        [Required(ErrorMessage = "Age is required")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Hobbies are required")]
        public string[] Hobbies { get; set; } = new string[0];
    }
}
