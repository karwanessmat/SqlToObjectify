namespace SqlToObjectify.Test;

// --- DTOs used by tests ---

public class EmployeeDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int DepartmentId { get; set; }
}

public class DepartmentDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class DepartmentEmployeeCountDto
{
    public string? DepartmentName { get; set; }
    public int NumberOfEmployee { get; set; }
}

// DTO with fewer properties than the query returns (extra columns should be ignored)
public class EmployeeNameOnlyDto
{
    public string? Name { get; set; }
}

// DTO with more properties than the query returns (missing columns should stay default)
public class EmployeeExtendedDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int DepartmentId { get; set; }
    public string? Email { get; set; }  // not in DB — should remain null
    public int Score { get; set; }       // not in DB — should remain 0
}

// Enum for type conversion tests
public enum EmployeeCategory
{
    Regular = 1,
    Lead = 2,
    Manager = 3
}

public class EmployeeWithCategoryDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public EmployeeCategory DepartmentId { get; set; } // int column mapped to enum
}
