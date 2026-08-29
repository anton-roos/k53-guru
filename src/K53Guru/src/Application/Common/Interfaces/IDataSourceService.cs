using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace K53Guru.Application.Common.Interfaces;

public interface IDataSourceService<T>
{
    IReadOnlyList<T> DataSource { get; }
    Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>>? predicate, int? limit=null, CancellationToken cancellationToken = default);
    event Func<Task>? OnChange;
    Task InitializeAsync();
    Task RefreshAsync();
}
