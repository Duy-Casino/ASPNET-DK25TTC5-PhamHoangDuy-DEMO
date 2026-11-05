namespace OnlineLearning.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Instructor = "Instructor";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> All = [Admin, Instructor, Student];
}
