using System.Net;

namespace Utilities;

// bars from CSV

public partial class Bars : TestBase
{
    public TestContext TestContext { get; set; }

    private static readonly string[] csvRows = [
        "date,open,high,low,close,volume",
        "2017-01-04,213.16,214.22,213.15,214.06,83348752",
        "2017-01-03,212.61,213.35,211.52,212.8,96708880"
    ];

    [TestMethod]
    public void FromCsv()
    {
        IReadOnlyList<Bar> bars = Bar.FromCsv(Path.Combine("TestData", "quotes", "default.csv"));

        bars.Should().HaveCount(502);
        bars.Should().Equal(Bars);
    }

    [TestMethod]
    public void FromCsvWithByteOrderMarkAndUsDates()
    {
        IReadOnlyList<Bar> bars = Bar.FromCsv(Path.Combine("TestData", "quotes", "msft.csv"));

        bars.Should().Equal(Data.BarsFromCsv("msft.csv"));
        bars[0].Should().Be(new Bar(new DateTime(1990, 1, 2), 0.378m, 0.385m, 0.373m, 0.385m, 0m));
    }

    [TestMethod]
    public async Task FromCsvAsync()
    {
        Uri uri = new("https://example.com/bars.csv");
        using StubHandler handler = new(string.Join("\r\n", csvRows) + "\r\n");
        using HttpClient client = new(handler, disposeHandler: false);

        IReadOnlyList<Bar> bars = await Bar
            .FromCsvAsync(uri, client, TestContext.CancellationToken)
            .ConfigureAwait(true);

        handler.RequestUri.Should().Be(uri);
        bars.Should().HaveCount(2);
        bars[0].Should().Be(new Bar(new DateTime(2017, 1, 3), 212.61m, 213.35m, 211.52m, 212.8m, 96708880m));
        bars[1].Timestamp.Should().Be(new DateTime(2017, 1, 4));
    }

    [TestMethod]
    public void FromCsvWithoutHeader()
    {
        IReadOnlyList<Bar> bars = Bar.ParseCsv(csvRows.Skip(1));

        bars.Should().Equal(Bar.ParseCsv(csvRows));
    }

    [TestMethod]
    public void FromCsvSkipsBlankLines()
    {
        IReadOnlyList<Bar> bars = Bar.ParseCsv(["", csvRows[0], "  ", csvRows[1], "", csvRows[2], ""]);

        bars.Should().Equal(Bar.ParseCsv(csvRows));
    }

    [TestMethod]
    public void FromCsvWithTimestampHeader()
    {
        IReadOnlyList<Bar> bars = Bar.ParseCsv(["Timestamp,Open,High,Low,Close,Volume", csvRows[1]]);

        bars.Should().ContainSingle();
    }

    [TestMethod]
    public void FromCsvWithHeaderOutOfOrder()
        => FluentActions
            .Invoking(static () => Bar.ParseCsv(["date,open,low,high,close,volume", csvRows[1]]))
            .Should()
            .ThrowExactly<FormatException>()
            .WithMessage("*not in date, open, high, low, close, volume order*");

    [TestMethod]
    public void FromCsvWithMissingColumn()
        => FluentActions
            .Invoking(static () => Bar.ParseCsv([csvRows[0], csvRows[1], "2017-01-05,214.75,216.05,213.81,215.62"]))
            .Should()
            .ThrowExactly<FormatException>()
            .WithMessage("CSV line 3 has 5 columns*");

    [TestMethod]
    public void FromCsvWithBadValue()
        => FluentActions
            .Invoking(static () => Bar.ParseCsv([csvRows[0], "2017-01-05,214.75,216.05,213.81,n/a,83348752"]))
            .Should()
            .ThrowExactly<FormatException>()
            .WithMessage("CSV line 2 has an invalid close value 'n/a'.");

    [TestMethod]
    public void FromCsvWithBadDate()
        => FluentActions
            .Invoking(static () => Bar.ParseCsv([csvRows[0], csvRows[1], "Jan 99th,214.75,216.05,213.81,215.62,83348752"]))
            .Should()
            .ThrowExactly<FormatException>()
            .WithMessage("CSV line 3 has an invalid date 'Jan 99th'.");

    [TestMethod]
    public async Task FromCsvWithNullArguments()
    {
        FluentActions
            .Invoking(static () => Bar.FromCsv(null))
            .Should()
            .ThrowExactly<ArgumentNullException>();

        FluentActions
            .Invoking(static () => Bar.FromCsv(string.Empty))
            .Should()
            .ThrowExactly<ArgumentException>();

        await FluentActions
            .Awaiting(() => Bar.FromCsvAsync(null, cancellationToken: TestContext.CancellationToken))
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>()
            .ConfigureAwait(true);
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        internal Uri RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(content)
            });
        }
    }
}
