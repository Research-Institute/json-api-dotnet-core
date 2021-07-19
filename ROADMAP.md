# JsonApiDotNetCore roadmap

This document provides an overview of the direction this project is heading and lists what we intend to work on in the near future.

> Disclaimer: This is an open source project. The available time of our contributors varies and therefore we do not plan release dates. This document expresses our current intent, which may change over time.

## v4.x

We've completed active development on v4.x, but we'll still fix important bugs or add small enhancements on request that don't require breaking changes nor lots of testing.

## v5.x

The need for breaking changes has blocked several efforts in the v4.x release, so now that we're starting work on v5, we're going to catch up.

- Remove Resource Hooks [#1025](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1025)
- Update to .NET/EFCORE 5 [#1026](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1026)
- Native many-to-many [#935](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/935)
- Refactorings [#1027](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1027) [#944](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/944)
- Instrumentation [#1032](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1032)
- Support System.Text.Json [#664](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/664) [#999](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/999) [#233](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/233)
- Optimize IIdentifiable to ResourceObject conversion [#1028](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1028) [#1024](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1024)
- Nullable reference types [#1029](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1029)
- Tweak trace logging [#1033](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1033)

Aside from the list above, we have interest in the following topics. It's too soon yet to decide whether they'll make it into v5.x or in a later major version.

- Optimized delete to-many [#1030](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1030)
- Improved paging links [#1010](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1010)
- Extract annotations into separate package [#730](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/730)
- Fluent API [#776](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/776)
- Auto-generated controllers [#732](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/732) [#365](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/365)
- Configuration validation [#170](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/170)
- Optimistic concurrency [#1004](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/1004)
- Resource inheritance [#844](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/844)
- OpenAPI (Swagger) [#259](https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/259)
- Idempotency

## Feedback

The best way to give feedback is to create new issues or upvote/downvote existing ones.
Please give us feedback that will give us insight on the following points:

* Existing features that are missing some capability or otherwise don't work well enough.
* Missing features that should be added to the product.
* Design choices for a feature that is currently in-progress.
