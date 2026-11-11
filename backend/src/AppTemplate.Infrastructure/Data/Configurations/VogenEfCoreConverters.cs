using AppTemplate.Core.Aggregates.UserAggregate;
using Vogen;

namespace AppTemplate.Infrastructure.Data.Configurations;

[EfCoreConverter<UserId>]
[EfCoreConverter<UserName>]
internal partial class VogenEfCoreConverters;
