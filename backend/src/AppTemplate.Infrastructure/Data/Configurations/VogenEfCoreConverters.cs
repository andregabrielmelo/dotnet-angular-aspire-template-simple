using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.UserAggregate;
using Vogen;

namespace AppTemplate.Infrastructure.Data.Configurations;

[EfCoreConverter<RefreshTokenId>]
[EfCoreConverter<UserId>]
[EfCoreConverter<UserName>]
internal partial class VogenEfCoreConverters;
