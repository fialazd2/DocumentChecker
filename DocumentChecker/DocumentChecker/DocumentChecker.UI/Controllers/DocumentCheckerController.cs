using DocumentChecker.Models;
using DocumentChecker.UI.Parsers;
using DocumentChecker.UI.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DocumentChecker.UI.Controllers
{
    public class DocumentCheckerController
    {
        public DocumentCheckerForm View { get; internal set; }

        /// <summary>
        /// Pošle paralelní dotazy na MVČR, zpracuje výsledek případně updatuje view
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task CheckDocumentAsync(DocumentCheckerModelInput model)
        {
            List<Task<XDocument>> tasks = CreateAndStart(model);

            var allTasksResult = Task.WhenAll(tasks.ToArray());
            XDocument[] resultDocuments;
            try
            {
                resultDocuments = await allTasksResult;
                List<DocumentCheckerModelOutput> xmlResultParsers = new XmlResultParser(resultDocuments).Parse();

                var registeredAnswer = xmlResultParsers.FirstOrDefault(result => result.Result == Result.Registered);
                if (registeredAnswer != null)
                {
                    View.UpdateView(registeredAnswer);
                    return;
                }

                var nonRegisteredAnswer = xmlResultParsers.FirstOrDefault(result => result.Result == Result.NotRegistered);
                if (nonRegisteredAnswer != null)
                {
                    View.UpdateView(nonRegisteredAnswer);
                    return;
                }

                var distinctErrors = xmlResultParsers.Where(parser => parser.Result == Result.Error).Select(parser => parser.Message).Distinct();

                View.UpdateView(GetErrors(distinctErrors));
            }
            catch (Exception)
            {
                if (allTasksResult.Exception != null)
                {
                    var distinctErrors = allTasksResult.Exception.InnerExceptions.Select(exception => exception.Message).Distinct();
                    View.UpdateView(GetErrors(distinctErrors));
                }
            }
        }


        /// <summary>
        /// Vyttvoří seznam tasků sloužící k paralelnímu dotazování MVČR
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private List<Task<XDocument>> CreateAndStart(DocumentCheckerModelInput model)
        {
            List<Task<XDocument>> tasks = new List<Task<XDocument>>();

            string[] documentTypes = new[] { "0", "4", "5", "6" };
            foreach (string documentType in documentTypes)
            {
                var task = new Task<XDocument>(() =>
                {
                    return GetResponse(string.Format(Properties.Settings.Default.NeplatneDokladyUrl, model.DocumentNumber, documentType));
                });

                tasks.Add(task);
                task.Start();
            }

            return tasks;
        }

        private DocumentCheckerModelOutput GetErrors(IEnumerable<string> distinctErrors)
        {
            return new DocumentCheckerModelOutput() { Message = string.Join(Environment.NewLine, distinctErrors), Result = Result.Error };
        }

        private XDocument GetResponse(string url)
        {
            return XDocument.Load(url, LoadOptions.None);
        }
    }
}
