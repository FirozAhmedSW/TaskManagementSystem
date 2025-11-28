namespace TaskManagementSystem.Models
{
    public class CSVUser
    {
        public List<User> MatchedUsers { get; set; } = new List<User>();
        public List<User> NonMatchedUsers { get; set; } = new List<User>();
    }
}