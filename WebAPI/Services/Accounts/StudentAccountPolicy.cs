namespace WebAPI.Services.Accounts
{
    public static class StudentAccountPolicy
    {
        private static readonly DateOnly SchoolYearStartDate = new(2000, 9, 15);

        public static int DetermineSchoolYearStart(DateTime referenceUtc)
        {
            var date = DateOnly.FromDateTime(referenceUtc);
            var currentYearBoundary = new DateOnly(date.Year, SchoolYearStartDate.Month, SchoolYearStartDate.Day);
            return date >= currentYearBoundary ? date.Year : date.Year - 1;
        }

        public static DateTime CalculateScheduledDeletionUtc(int gradeLevel, DateTime referenceUtc)
        {
            if (gradeLevel is < 1 or > 12)
                throw new ArgumentOutOfRangeException(nameof(gradeLevel), "Grade level must be between 1 and 12.");

            var schoolYearStart = DetermineSchoolYearStart(referenceUtc);
            return CalculateScheduledDeletionUtc(gradeLevel, schoolYearStart);
        }

        public static DateTime CalculateScheduledDeletionUtc(int gradeLevel, int schoolYearStart)
        {
            if (gradeLevel is < 1 or > 12)
                throw new ArgumentOutOfRangeException(nameof(gradeLevel), "Grade level must be between 1 and 12.");

            var completionYear = schoolYearStart + 1;
            var completionDate = gradeLevel switch
            {
                12 => new DateTime(completionYear, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                <= 3 => new DateTime(completionYear, 5, 29, 0, 0, 0, DateTimeKind.Utc),
                <= 6 => new DateTime(completionYear, 6, 12, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(completionYear, 6, 30, 0, 0, 0, DateTimeKind.Utc)
            };

            return completionDate.AddDays(1);
        }
    }
}
