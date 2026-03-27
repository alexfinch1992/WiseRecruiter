# WiseRecruiter.Tests - Automated Test Suite

Comprehensive test suite for the WiseRecruiter ATS application focusing on core business logic and critical workflows.

## Project Structure

```
WiseRecruiter.Tests/
├── Unit/
│   └── Services/
│       ├── ApplicationServiceTests.cs      # Unit tests for application pipeline logic
│       └── AnalyticsServiceTests.cs        # Unit tests for analytics aggregation
├── Integration/
│   ├── ApplicationFlowTests.cs             # End-to-end application workflows
│   └── AuthorizationTests.cs               # Authorization and access control
└── WiseRecruiter.Tests.csproj              # Test project configuration
```

## Running Tests

### Run All Tests
```powershell
dotnet test
```

### Run Specific Test Class
```powershell
dotnet test --filter "FullyQualifiedName~ApplicationServiceTests"
```

### Run by Test Category
```powershell
# Unit tests only
dotnet test --filter "FullyQualifiedName~Unit"

# Integration tests only
dotnet test --filter "FullyQualifiedName~Integration"
```

### Run Single Test
```powershell
dotnet test --filter "DisplayName=ApplicationServiceTests::CreateApplicationAsync_WithNoStageSpecified_AutoAssignsToFirstStage"
```

### Run with Verbose Output
```powershell
dotnet test --verbosity detailed
```

## Test Coverage

### Unit Tests - ApplicationService
- ✅ Auto-stage assignment when creating applications
- ✅ Stage validation during transitions
- ✅ Preventing moves to stages belonging to different jobs
- ✅ Sorting applications by name, applied date, and stage
- ✅ Error handling for non-existent applications

**File:** `Unit/Services/ApplicationServiceTests.cs` (10 tests)

### Unit Tests - AnalyticsService
- ✅ Stage breakdown with correct candidate counts
- ✅ Percentage calculations for stage distribution
- ✅ Handling empty datasets gracefully
- ✅ Job statistics and aggregation
- ✅ Application trends and cumulative totals
- ✅ Job-specific analytics
- ✅ Exception handling for invalid job IDs

**File:** `Unit/Services/AnalyticsServiceTests.cs` (8 tests)

### Integration Tests - Application Flow
- ✅ Complete application creation workflow
- ✅ Multi-stage progression tracking
- ✅ Multiple applications with independent pipelines
- ✅ End-to-end candidate journey

**File:** `Integration/ApplicationFlowTests.cs` (4 tests)

### Integration Tests - Authorization
- ✅ AdminController requires authorization
- ✅ Custom "AdminAuth" scheme is enforced
- ✅ Protected action methods
- ✅ No public bypass for admin endpoints

**File:** `Integration/AuthorizationTests.cs` (7 tests)

## Key Features

### High-Value Testing Focus
- Tests only meaningful business logic, not trivial getters/setters
- Focuses on workflows that could break or impact users
- No excessive boilerplate or stubbed tests

### InMemory Database Testing
- Uses EF Core InMemory for fast, isolated tests
- No database setup or teardown required
- Each test gets its own isolated database context

### Real Service Contracts
- Tests use actual service methods and real EF Core queries
- Validates aggregation logic, stage transitions, and workflows
- Tests edge cases like empty datasets and validation failures

### Moq Integration Ready
- Framework configured for mocking (not used in first release)
- Can be extended to mock external dependencies

### FluentAssertions
- Clear, readable assertions
- Better error messages on test failures
- Chainable fluent syntax for complex validations

## Framework & Dependencies

- **xUnit** - Fast, modern test framework
- **Moq** - Mocking framework (available for future use)
- **FluentAssertions** - Clear assertion syntax
- **EF Core InMemory** - Lightweight database for tests
- **.NET 9.0** - Target framework

## Test Execution Strategy

### Unit Tests (18 tests)
Run in milliseconds with InMemory database. No external dependencies.

### Integration Tests (11 tests)  
Test complete workflows using real service interactions. Still use InMemory for speed.

**Total: 29 high-value tests**

## Continuous Integration

Add to CI pipeline:

```yaml
- name: Run Tests
  run: dotnet test WiseRecruiter.Tests --logger trx --results-directory ./test-results
```

## Future Enhancements

- [ ] Mock external file upload service
- [ ] Add performance benchmarks for analytics queries
- [ ] Integration tests with real SQL Server using test containers
- [ ] API endpoint tests for controllers
- [ ] Document storage tests

## Debugging Tests

### With Verbose Output
```powershell
dotnet test --verbosity detailed
```

### Using Test Explorer in VS Code
Install the "Test Explorer UI" extension for graphical test execution.

### Attaching Debugger
Place breakpoints in test files and run:
```powershell
dotnet test --debugger
```

## Adding New Tests

1. Create test class in appropriate folder (Unit/Services or Integration)
2. Inherit test naming: `ClassNameTests`
3. Use descriptive test names: `MethodName_Scenario_ExpectedOutcome`
4. Setup data using InMemory context
5. Follow AAA pattern: Arrange → Act → Assert

Example template:
```csharp
[Fact]
public async Task MyMethod_WhenCondition_ExpectedBehavior()
{
    // Arrange
    var context = CreateInMemoryContext();
    var service = new MyService(context);
    
    // Act
    var result = await service.MyMethod();
    
    // Assert
    result.Should().Be(expected);
}
```

## Notes

- All tests are isolated and can run in any order
- No external file I/O (tests are sandboxed in memory)
- Each test cleans up automatically through InMemory database disposal
- Tests validate business rules, not implementation details
