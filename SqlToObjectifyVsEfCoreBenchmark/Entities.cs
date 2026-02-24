public sealed class Person
{
    public int Id { get; set; }                 // Identity
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
    public string City { get; set; } = "";
}

public sealed class PersonDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public int Age { get; set; }
    public string City { get; set; } = "";
}
