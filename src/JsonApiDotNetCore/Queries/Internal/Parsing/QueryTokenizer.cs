using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using JetBrains.Annotations;

namespace JsonApiDotNetCore.Queries.Internal.Parsing
{
    [PublicAPI]
    public sealed class QueryTokenizer
    {
        public static readonly IReadOnlyDictionary<char, TokenKind> SingleCharacterToTokenKinds = new ReadOnlyDictionary<char, TokenKind>(
            new Dictionary<char, TokenKind>
            {
                ['('] = TokenKind.OpenParen,
                [')'] = TokenKind.CloseParen,
                ['['] = TokenKind.OpenBracket,
                [']'] = TokenKind.CloseBracket,
                [','] = TokenKind.Comma,
                [':'] = TokenKind.Colon,
                ['-'] = TokenKind.Minus
            });

        private readonly string _source;
        private readonly StringBuilder _textBuffer = new();
        private int _offset;
        private bool _isInQuotedSection;

        public QueryTokenizer(string source)
        {
            ArgumentGuard.NotNull(source, nameof(source));

            _source = source;
        }

        public IEnumerable<Token> EnumerateTokens()
        {
            _textBuffer.Clear();
            _isInQuotedSection = false;
            _offset = 0;

            while (_offset < _source.Length)
            {
                char ch = _source[_offset];

                if (ch == '\'')
                {
                    if (_isInQuotedSection)
                    {
                        char? peeked = PeekChar();

                        if (peeked == '\'')
                        {
                            _textBuffer.Append(ch);
                            _offset += 2;
                            continue;
                        }

                        _isInQuotedSection = false;

                        Token literalToken = ProduceTokenFromTextBuffer(true);
                        yield return literalToken;
                    }
                    else
                    {
                        if (_textBuffer.Length > 0)
                        {
                            throw new QueryParseException("Unexpected ' outside text.");
                        }

                        _isInQuotedSection = true;
                    }
                }
                else
                {
                    TokenKind? singleCharacterTokenKind = _isInQuotedSection ? null : TryGetSingleCharacterTokenKind(ch);

                    if (singleCharacterTokenKind != null && !IsMinusInsideText(singleCharacterTokenKind.Value))
                    {
                        Token identifierToken = ProduceTokenFromTextBuffer(false);

                        if (identifierToken != null)
                        {
                            yield return identifierToken;
                        }

                        yield return new Token(singleCharacterTokenKind.Value);
                    }
                    else
                    {
                        if (_textBuffer.Length == 0 && ch == ' ')
                        {
                            throw new QueryParseException("Unexpected whitespace.");
                        }

                        _textBuffer.Append(ch);
                    }
                }

                _offset++;
            }

            if (_isInQuotedSection)
            {
                throw new QueryParseException("' expected.");
            }

            Token lastToken = ProduceTokenFromTextBuffer(false);

            if (lastToken != null)
            {
                yield return lastToken;
            }
        }

        private bool IsMinusInsideText(TokenKind kind)
        {
            return kind == TokenKind.Minus && _textBuffer.Length > 0;
        }

        private char? PeekChar()
        {
            return _offset + 1 < _source.Length ? _source[_offset + 1] : null;
        }

        private static TokenKind? TryGetSingleCharacterTokenKind(char ch)
        {
            return SingleCharacterToTokenKinds.ContainsKey(ch) ? SingleCharacterToTokenKinds[ch] : null;
        }

        private Token ProduceTokenFromTextBuffer(bool isQuotedText)
        {
            if (isQuotedText || _textBuffer.Length > 0)
            {
                string text = _textBuffer.ToString();
                _textBuffer.Clear();
                return new Token(isQuotedText ? TokenKind.QuotedText : TokenKind.Text, text);
            }

            return null;
        }
    }
}
