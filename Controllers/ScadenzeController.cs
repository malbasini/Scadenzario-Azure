using System.Net.Http.Headers;
using AspNetCore.ReCaptcha;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Scadenzario.Customizations.Authorization;
using Scadenzario.Models.Enums;
using Scadenzario.Models.InputModels.Ricevute;
using Scadenzario.Models.InputModels.Scadenze;
using Scadenzario.Models.Services.Applications.Scadenze;
using Scadenzario.Models.Utility;
using Scadenzario.Models.ViewModels;
using Scadenzario.Models.ViewModels.Ricevute;
using Scadenzario.Models.ViewModels.Scadenze;

namespace Scadenzario.Controllers
{
    [Authorize]
    public class ScadenzeController : Controller
    {
        private readonly IScadenzeService service;
        private readonly IRicevuteService _ricevute;
        private readonly IWebHostEnvironment _environment;
        public static List<RicevutaCreateInputModel>? Ricevute { get; private set;}
        public ScadenzeController(ICachedScadenzeService service, IRicevuteService ricevute,IWebHostEnvironment environment)
        {
            this.service = service;
            _ricevute = ricevute;
            _environment = environment;
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index(ScadenzaListInputModel input)
        {
            ViewData["Title"] = "Lista Scadenze";
            ListViewModel<ScadenzaViewModel> scadenze = await service.GetScadenzeAsync(input);

            ScadenzaListViewModel viewModel = new ScadenzaListViewModel
            {
                Scadenze = scadenze,
                Input = input
            };

            return View(viewModel);
        }
        public async Task<IActionResult> Detail(int id)
        {
            ViewData["Title"] = "Dettaglio Scadenza";
            ScadenzaDetailViewModel viewModel = await service.GetScadenzaAsync(id);
            return View(viewModel);
        }
        [HttpGet]
        [AuthorizeRole(Role.Administrator,Role.Editor)]
        public IActionResult Create()
        {
            ViewData["Title"] = "Nuova Scadenza";
            ScadenzaCreateInputModel inputModel = new ScadenzaCreateInputModel();
            inputModel.DataScadenza = DateTime.Now;
            inputModel.Beneficiari = service.GetBeneficiari();
            return View(inputModel);
        }
        [AuthorizeRole(Role.Administrator,Role.Editor)]
        [ValidateReCaptcha]
        [HttpPost]
        public async Task<IActionResult> Create(ScadenzaCreateInputModel inputModel)
        {
            inputModel.Beneficiari = service.GetBeneficiari();
            if(ModelState.IsValid)
            {
                await service.CreateScadenzaAsync(inputModel);
                TempData["ConfirmationMessage"] = "Ok! la tua scadenza è stata creata, ora perché non inserisci anche gli altri dati?";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                
                IEnumerable<ModelError> allErrors = ModelState.Values.SelectMany(v => v.Errors);
                Console.WriteLine(allErrors);
                ViewData["Title"] = "Nuova Scadenza";
                return View(inputModel); 
            }
              
        }
        [AuthorizeRole(Role.Administrator,Role.Editor)]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            TempData["IDScadenza"]=id; 
            ViewData["Title"] = "Aggiorna Scadenza";
            ScadenzaEditInputModel inputModel = new ScadenzaEditInputModel();
            inputModel = await service.GetScadenzaForEditingAsync(id);
            inputModel.Denominazione=service.GetBeneficiarioById(inputModel.IdBeneficiario);
            inputModel.Beneficiari = service.GetBeneficiari();
            return View(inputModel);
        }
        [AuthorizeRole(Role.Administrator,Role.Editor)]
        [HttpPost]
        public async Task<IActionResult> Edit(ScadenzaEditInputModel inputModel)
        {
            inputModel.Denominazione=service.GetBeneficiarioById(inputModel.IdBeneficiario);
            if (ModelState.IsValid)
            {
                if(inputModel.DataPagamento.HasValue)
                    inputModel.GiorniRitardo=service.DateDiff(inputModel.DataScadenza,inputModel.DataPagamento.Value);
                else
                    inputModel.GiorniRitardo=service.DateDiff(inputModel.DataScadenza,DateTime.Now.Date);   
                await service.EditScadenzaAsync(inputModel);
                TempData["Message"] = "Aggiornamento effettuato correttamente".ToUpper();
                return RedirectToAction(nameof(Index),"Scadenze");
            }
            else
            {
                ViewData["Title"] = "Aggiorna Scadenza".ToUpper();
                inputModel.Beneficiari = service.GetBeneficiari();
                return View(inputModel);
            }

        }
        [AuthorizeRole(Role.Administrator)]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            ScadenzaDeleteInputModel inputModel = new ScadenzaDeleteInputModel();
            inputModel.IdScadenza=id;
            if(ModelState.IsValid)
            {
                await service.DeleteScadenzaAsync(inputModel);
                TempData["Message"] = "Cancellazione effettuata correttamente";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                ViewData["Title"] = "Elimina scadenza";
                return View(inputModel); 
            }
              
        }
         [HttpPost]
     public async Task<IActionResult> FileUpload()
     {
            var id = Convert.ToInt32(TempData["IDScadenza"]);
            ScadenzaEditInputModel inputModel = new();
            inputModel = await service.GetScadenzaForEditingAsync(id);
            var files = Request.Form.Files;
            var i = 0;
            string physicalWebRootPath = _environment.ContentRootPath;
            var path = String.Empty;
            if(OperatingSystem.IsWindows())
                path = physicalWebRootPath + "\\Upload";
            else if (OperatingSystem.IsLinux()|| OperatingSystem.IsMacOS())
                path = physicalWebRootPath + "/Upload";
            foreach (var file in files)
            {
                RicevutaCreateInputModel ricevuta = new RicevutaCreateInputModel();
                var fileName = ContentDispositionHeaderValue
                    .Parse(file.ContentDisposition)
                    .FileName;
                if (fileName != null)
                {
                    var filename = fileName
                        .Trim('"');
                    ricevuta.FileName=filename;
                    var fileType = file.ContentType;
                    var fileLenght = file.Length;
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    filename = System.IO.Path.Combine(path, filename);
                    using (FileStream fs = System.IO.File.Create(filename))
                    {
                        await file.CopyToAsync(fs);
                        await fs.FlushAsync();
                    }
                    i += 1;
                    ricevuta.FileType=fileType;
                    ricevuta.Path=filename;
                    ricevuta.IDScadenza=inputModel.IdScadenza;
                    ricevuta.Beneficiario=inputModel.Denominazione;
                    byte[] filedata = new byte[fileLenght];
                    using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        using (var reader = new BinaryReader(stream))
                        {
                            filedata = reader.ReadBytes((int)stream.Length);
                        }
                    } 
                    ricevuta.FileContent=filedata;
                }

                AddRicevuta(ricevuta);
            }
            //Gestione Ricevute
            if(Ricevute!=null)
                await _ricevute.CreateRicevutaAsync(Ricevute);
            Ricevute=null;
            string message = "Upload ed inserimento effettuati correttamente!";
            JsonResult result = new JsonResult(message);
            return result;
     }
     public static void AddRicevuta(RicevutaCreateInputModel ricevuta)
     {
            if(Ricevute==null)
                Ricevute = new();
            Ricevute.Add(ricevuta);
     }
     public async Task<IActionResult> Download(int Id)
     {
         var viewModel = await _ricevute.GetRicevutaAsync(Id);
         string filename = viewModel.Path;
         if (filename == null)
             throw new Exception("File name not found");

         var path = Path.Combine(
             Directory.GetCurrentDirectory(),
             "wwwroot", filename);

         var memory = new MemoryStream();
         using (var stream = new FileStream(path, FileMode.Open))
         {
             await stream.CopyToAsync(memory);
         }
         memory.Position = 0;
         return File(memory, Utility.GetContentType(path), Path.GetFileName(path));
     }
     public async Task<IActionResult> DeleteAllegato(int id, int idscadenza)
     {
         ScadenzaDetailViewModel viewModel = await service.GetScadenzaAsync(idscadenza);
         ViewData["Title"] = "Dettaglio Scadenza";
         RicevutaViewModel ricevutaViewModel = await _ricevute.GetRicevutaAsync(id);
         await _ricevute.DeleteRicevutaAsync(id);
         string filename = ricevutaViewModel.Path;
         if (filename == null)
             throw new Exception("File name not found");
         var path = Path.Combine(
             Directory.GetCurrentDirectory(),
             "wwwroot", filename);
         System.IO.File.Delete(path);
         viewModel.Ricevute = _ricevute.GetRicevute(idscadenza);
         TempData["Message"] = "Cancellazione effettuata correttamente";
         return RedirectToAction(nameof(System.Index),"Scadenze");
     }
    }
}