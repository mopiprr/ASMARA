using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data;

public static class DbUpdateExceptionExtensions
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
        => exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
