using System;

namespace RichTextConverter;

public class RichTextException : Exception
{
    private readonly string _errorMessage;
    private readonly string _errorText;

    public RichTextException(string message, string errorText = "") : base(message)
    {
        _errorMessage = message;
        _errorText = errorText;
    }

    public override string ToString()
    {
        return _errorMessage + " - [" + _errorText[..30] + "...]";
    }
}