using System;
using System.ComponentModel.DataAnnotations;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Reporting;

internal class Program
{
    private static void Main(string[] args)
    {
        // Load the DOCX template
        Document doc = new Document(@"C:\temp\Aspose\Mail merge destinations - Orders.docx");

        // Validate the document
        List<ValidationResult> validationResults = new List<ValidationResult>();
        var context = new ValidationContext(doc, null, null);
        bool isValid = Validator.TryValidateObject(doc, context, validationResults, true);

        if (isValid)
        {
            Console.WriteLine("The document is valid.");
        }
        else
        {
            Console.WriteLine("The document is invalid:");
            foreach (var validationResult in validationResults)
            {
                Console.WriteLine($" - {validationResult.ErrorMessage}");
            }
        }

        // Define some simple data for the mail merge (like a dictionary of values)
        var data = new
        {
            Name = "John Doe",
            Address = "1234 Main St",
            City = "New York",
            PostalCode = "10001"
        };

        // Use Aspose.Words' mail merge feature to insert the data into the document
        doc.MailMerge.Execute(new string[] { "Name", "Address", "City", "PostalCode" },
                              new object[] { data.Name, data.Address, data.City, data.PostalCode });

        // Save the document as a PDF file
        doc.Save(@"C:\temp\Aspose\OutputFile.pdf", SaveFormat.Pdf);

        Console.WriteLine("PDF generated successfully.");
    }
}

public class ValidateTagsAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return new ValidationResult("Document cannot be null.");
        }

        var document = (Document)value;
        var tags = new List<string>();
        foreach (Field field in document.Range.Fields)
        {
            tags.Add(field.GetFieldCode());
        }

        // Add your tag validation logic here
        if (!tags.Contains("YourTag"))
        {
            return new ValidationResult("Document does not contain the required tags.");
        }

        return ValidationResult.Success;
    }
}
