using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.InputValidation.ModelState
{
    public sealed class ModelStateValidationTests
        : IClassFixture<ExampleIntegrationTestContext<ModelStateValidationStartup<ModelStateDbContext>, ModelStateDbContext>>
    {
        private readonly ExampleIntegrationTestContext<ModelStateValidationStartup<ModelStateDbContext>, ModelStateDbContext> _testContext;

        public ModelStateValidationTests(ExampleIntegrationTestContext<ModelStateValidationStartup<ModelStateDbContext>, ModelStateDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<SystemDirectoriesController>();
            testContext.UseController<SystemFilesController>();
        }

        [Fact]
        public async Task Cannot_create_resource_with_omitted_required_attribute()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new
                    {
                        isCaseSensitive = true
                    }
                }
            };

            const string route = "/systemDirectories";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Input validation failed.");
            error.Detail.Should().Be("The Name field is required.");
            error.Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task Cannot_create_resource_with_null_for_required_attribute_value()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new
                    {
                        name = (string)null,
                        isCaseSensitive = true
                    }
                }
            };

            const string route = "/systemDirectories";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Input validation failed.");
            error.Detail.Should().Be("The Name field is required.");
            error.Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task Cannot_create_resource_with_invalid_attribute_value()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new
                    {
                        name = "!@#$%^&*().-",
                        isCaseSensitive = true
                    }
                }
            };

            const string route = "/systemDirectories";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Input validation failed.");
            error.Detail.Should().Be("The field Name must match the regular expression '^[\\w\\s]+$'.");
            error.Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task Can_create_resource_with_valid_attribute_value()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new
                    {
                        name = "Projects",
                        isCaseSensitive = true
                    }
                }
            };

            const string route = "/systemDirectories";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["name"].Should().Be("Projects");
            responseDocument.SingleData.Attributes["isCaseSensitive"].Should().Be(true);
        }

        [Fact]
        public async Task Cannot_create_resource_with_multiple_violations()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new
                    {
                        sizeInBytes = -1
                    }
                }
            };

            const string route = "/systemDirectories";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(3);

            Error error1 = responseDocument.Errors[0];
            error1.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error1.Title.Should().Be("Input validation failed.");
            error1.Detail.Should().Be("The Name field is required.");
            error1.Source.Pointer.Should().Be("/data/attributes/name");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error2.Title.Should().Be("Input validation failed.");
            error2.Detail.Should().Be("The field SizeInBytes must be between 0 and 9223372036854775807.");
            error2.Source.Pointer.Should().Be("/data/attributes/sizeInBytes");

            Error error3 = responseDocument.Errors[2];
            error3.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error3.Title.Should().Be("Input validation failed.");
            error3.Detail.Should().Be("The IsCaseSensitive field is required.");
            error3.Source.Pointer.Should().Be("/data/attributes/isCaseSensitive");
        }

        [Fact]
        public async Task Can_create_resource_with_annotated_relationships()
        {
            // Arrange
            var parentDirectory = new SystemDirectory
            {
                Name = "Shared",
                IsCaseSensitive = true
            };

            var subdirectory = new SystemDirectory
            {
                Name = "Open Source",
                IsCaseSensitive = true
            };

            var file = new SystemFile
            {
                FileName = "Main.cs",
                SizeInBytes = 100
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.AddRange(parentDirectory, subdirectory);
                dbContext.Files.Add(file);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new
                    {
                        name = "Projects",
                        isCaseSensitive = true
                    },
                    relationships = new
                    {
                        subdirectories = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemDirectories",
                                    id = subdirectory.StringId
                                }
                            }
                        },
                        files = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemFiles",
                                    id = file.StringId
                                }
                            }
                        },
                        parent = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = parentDirectory.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/systemDirectories";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["name"].Should().Be("Projects");
            responseDocument.SingleData.Attributes["isCaseSensitive"].Should().Be(true);
        }

        [Fact]
        public async Task Can_add_to_annotated_ToMany_relationship()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            var file = new SystemFile
            {
                FileName = "Main.cs",
                SizeInBytes = 100
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(directory, file);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "systemFiles",
                        id = file.StringId
                    }
                }
            };

            string route = $"/systemDirectories/{directory.StringId}/relationships/files";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_update_resource_with_omitted_required_attribute_value()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new
                    {
                        sizeInBytes = 100
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_update_resource_with_null_for_required_attribute_value()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new
                    {
                        name = (string)null
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Input validation failed.");
            error.Detail.Should().Be("The Name field is required.");
            error.Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task Cannot_update_resource_with_invalid_attribute_value()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new
                    {
                        name = "!@#$%^&*().-"
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Input validation failed.");
            error.Detail.Should().Be("The field Name must match the regular expression '^[\\w\\s]+$'.");
            error.Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task Cannot_update_resource_with_invalid_ID()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = -1,
                    attributes = new
                    {
                        name = "Repositories"
                    },
                    relationships = new
                    {
                        subdirectories = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemDirectories",
                                    id = -1
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/systemDirectories/-1";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(2);

            Error error1 = responseDocument.Errors[0];
            error1.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error1.Title.Should().Be("Input validation failed.");
            error1.Detail.Should().Be("The field Id must match the regular expression '^[0-9]+$'.");
            error1.Source.Pointer.Should().Be("/data/attributes/id");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error2.Title.Should().Be("Input validation failed.");
            error2.Detail.Should().Be("The field Id must match the regular expression '^[0-9]+$'.");
            error2.Source.Pointer.Should().Be("/data/attributes/Subdirectories[0].Id");
        }

        [Fact]
        public async Task Can_update_resource_with_valid_attribute_value()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new
                    {
                        name = "Repositories"
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_update_resource_with_annotated_relationships()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = false,
                Subdirectories = new List<SystemDirectory>
                {
                    new()
                    {
                        Name = "C#",
                        IsCaseSensitive = false
                    }
                },
                Files = new List<SystemFile>
                {
                    new()
                    {
                        FileName = "readme.txt"
                    }
                },
                Parent = new SystemDirectory
                {
                    Name = "Data",
                    IsCaseSensitive = false
                }
            };

            var otherParent = new SystemDirectory
            {
                Name = "Shared",
                IsCaseSensitive = false
            };

            var otherSubdirectory = new SystemDirectory
            {
                Name = "Shared",
                IsCaseSensitive = false
            };

            var otherFile = new SystemFile
            {
                FileName = "readme.md"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.AddRange(directory, otherParent, otherSubdirectory);
                dbContext.Files.Add(otherFile);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new
                    {
                        name = "Project Files"
                    },
                    relationships = new
                    {
                        subdirectories = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemDirectories",
                                    id = otherSubdirectory.StringId
                                }
                            }
                        },
                        files = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemFiles",
                                    id = otherFile.StringId
                                }
                            }
                        },
                        parent = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = otherParent.StringId
                            }
                        }
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_update_resource_with_multiple_self_references()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = false
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new
                    {
                        name = "Project files"
                    },
                    relationships = new
                    {
                        self = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = directory.StringId
                            }
                        },
                        alsoSelf = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = directory.StringId
                            }
                        }
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_update_resource_with_collection_of_self_references()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = false
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new
                    {
                        name = "Project files"
                    },
                    relationships = new
                    {
                        subdirectories = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemDirectories",
                                    id = directory.StringId
                                }
                            }
                        }
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_replace_annotated_ToOne_relationship()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true,
                Parent = new SystemDirectory
                {
                    Name = "Data",
                    IsCaseSensitive = true
                }
            };

            var otherParent = new SystemDirectory
            {
                Name = "Data files",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.AddRange(directory, otherParent);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = otherParent.StringId
                }
            };

            string route = "/systemDirectories/" + directory.StringId + "/relationships/parent";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_replace_annotated_ToMany_relationship()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true,
                Files = new List<SystemFile>
                {
                    new()
                    {
                        FileName = "Main.cs"
                    },
                    new()
                    {
                        FileName = "Program.cs"
                    }
                }
            };

            var otherFile = new SystemFile
            {
                FileName = "EntryPoint.cs"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                dbContext.Files.Add(otherFile);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "systemFiles",
                        id = otherFile.StringId
                    }
                }
            };

            string route = "/systemDirectories/" + directory.StringId + "/relationships/files";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_remove_from_annotated_ToMany_relationship()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true,
                Files = new List<SystemFile>
                {
                    new()
                    {
                        FileName = "Main.cs",
                        SizeInBytes = 100
                    }
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(directory);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new object[0]
            };

            string route = $"/systemDirectories/{directory.StringId}/relationships/files";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }
    }
}
