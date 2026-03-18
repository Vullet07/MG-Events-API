using WebAPI.Services.Accounts;

namespace MGEvents.Tests.Services;

public class StudentAccountPolicyTests
{
    [Theory]
    [InlineData("2026-10-01T00:00:00Z", 12, "2027-05-16T00:00:00Z")]
    [InlineData("2026-10-01T00:00:00Z", 2, "2027-05-30T00:00:00Z")]
    [InlineData("2026-10-01T00:00:00Z", 5, "2027-06-13T00:00:00Z")]
    [InlineData("2026-10-01T00:00:00Z", 11, "2027-07-01T00:00:00Z")]
    public void CalculateScheduledDeletionUtc_UsesExpectedGraduationWindow(
        string referenceRaw,
        int gradeLevel,
        string expectedRaw)
    {
        var reference = DateTime.Parse(referenceRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        var expected = DateTime.Parse(expectedRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        var result = StudentAccountPolicy.CalculateScheduledDeletionUtc(gradeLevel, reference);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2026-09-14T12:00:00Z", 2025)]
    [InlineData("2026-09-15T00:00:00Z", 2026)]
    [InlineData("2027-03-01T08:30:00Z", 2026)]
    public void DetermineSchoolYearStart_RespectsSeptember15Boundary(string referenceRaw, int expectedSchoolYearStart)
    {
        var reference = DateTime.Parse(referenceRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        var result = StudentAccountPolicy.DetermineSchoolYearStart(reference);

        Assert.Equal(expectedSchoolYearStart, result);
    }
}
