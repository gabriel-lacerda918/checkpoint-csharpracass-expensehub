namespace ExpenseHub.Api.Domain;

/// <summary>
/// Optional classification of an expense.
/// </summary>
internal sealed class ExpenseCategory
{
    private ExpenseCategory()
    {
        Name = string.Empty;
    }

    public ExpenseCategory(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }
}
