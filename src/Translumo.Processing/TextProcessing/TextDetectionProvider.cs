using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Translumo.Infrastructure.Constants;
using Translumo.Infrastructure.Language;
using Translumo.OCR;
using Translumo.Processing.Configuration;
using Translumo.Processing.Exceptions;
using static System.Threading.Tasks.Task;

namespace Translumo.Processing.TextProcessing
{
    public class TextDetectionProvider : IDisposable
    {
        public Languages Language
        {
            get => _textValidityPredictor.Language;
            set
            {
                _textValidityPredictor.Language = value;
                _languageDescriptor = _languageService.GetLanguageDescriptor(value);
            }
        }

        private LanguageDescriptor _languageDescriptor;

        private readonly TextValidityPredictor _textValidityPredictor;
        private readonly LanguageService _languageService;
        private readonly TextProcessingConfiguration _configuration;
        
        public TextDetectionProvider(TextValidityPredictor textValidityPredictor, LanguageService languageService, 
            TextProcessingConfiguration configuration)
        {
            this._textValidityPredictor = textValidityPredictor;
            this._languageService = languageService;
            this._configuration = configuration;
        }

        public virtual TextDetectionResult GetText(IOCREngine ocrEngine, byte[] img)
        {
            try
            {
                var detectedLines = ocrEngine.GetTextLines(img);
                var resultText = PreProcessTextLines(detectedLines);
                var scorePrediction = _textValidityPredictor.Predict(detectedLines, out var validatedText);

                return new TextDetectionResult(ocrEngine, _languageDescriptor)
                {
                    ValidityScore = scorePrediction,
                    Text = resultText,
                    ValidatedText = validatedText
                };
            }
            catch (Exception ex)
            {
                throw new TextDetectionException("Text detection failed", ocrEngine.GetType(), ex);
            }
        }

        public virtual Task<TextDetectionResult> GetTextAsync(IOCREngine ocrEngine, byte[] img)
        {
            return Factory.StartNew(() => GetText(ocrEngine, img));
        }

        private string PreProcessTextLines(IEnumerable<string> textLines)
        {
            var lines = textLines?
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToList();

            if (lines == null || lines.Count == 0)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (i > 0)
                {
                    if (sb.Length > 0 && sb[^1] == '-' && char.IsLetter(line[0]))
                    {
                        sb.Length--;
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(line);
            }

            return RegexStorage.MultipleSpacesRegex.Replace(sb.ToString(), " ").Trim();
        }

        public void Dispose()
        {
            _textValidityPredictor.Dispose();
        }
    }
}
