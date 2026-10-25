using RocketExtensions.Models;
using Xunit;

namespace RFRocketLibrary.Tests
{
    // These exercises run the parser on a plain test host: only value types are parsed here,
    // so no game assets are needed (asset-backed paths require a running server).
    public class StringTypeConverterTests
    {
        [Theory]
        [InlineData("true", true)]
        [InlineData("on", true)]
        [InlineData("1", true)]
        [InlineData("false", false)]
        [InlineData("off", false)]
        [InlineData("0", false)]
        public void Parse_Bool_AcceptsDocumentedAliases(string input, bool expected)
        {
            var result = StringTypeConverter.Parse<bool>(input, out var value);
            Assert.Equal(EParseResult.Parsed, result);
            Assert.Equal(expected, value);
        }

        [Fact]
        public void Parse_Bool_RejectsUnknownWords()
        {
            var result = StringTypeConverter.Parse<bool>("maybe", out _);
            Assert.Equal(EParseResult.ParseFailed, result);
        }

        [Fact]
        public void Parse_Int_ParsesValidAndRejectsInvalid()
        {
            Assert.Equal(EParseResult.Parsed, StringTypeConverter.Parse<int>("123", out var parsed));
            Assert.Equal(123, parsed);

            Assert.Equal(EParseResult.ParseFailed, StringTypeConverter.Parse<int>("12x3", out _));
        }

        [Fact]
        public void Parse_UShort_ParsesValidAndRejectsNegative()
        {
            Assert.Equal(EParseResult.Parsed, StringTypeConverter.Parse<ushort>("65535", out var parsed));
            Assert.Equal((ushort) 65535, parsed);

            Assert.Equal(EParseResult.ParseFailed, StringTypeConverter.Parse<ushort>("-1", out _));
        }

        [Fact]
        public void Parse_String_PassesThrough()
        {
            Assert.Equal(EParseResult.Parsed, StringTypeConverter.Parse<string>("hello world", out var value));
            Assert.Equal("hello world", value);
        }
    }
}
