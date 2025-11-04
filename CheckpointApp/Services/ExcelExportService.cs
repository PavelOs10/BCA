using System.Collections.Generic;
using System.Threading.Tasks;
using CheckpointApp.Models;
using ClosedXML.Excel;
using System.Linq;

namespace CheckpointApp.Services
{
    public class ExcelExportService
    {
        // --- ИСПРАВЛЕНИЕ: Метод сделан статическим, так как не использует состояние объекта ---
        public static Task ExportCrossingsAsync(IEnumerable<Crossing> crossings, string filePath)
        {
            return Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Журнал пересечений");

                var headers = new[]
                {
                    "ID", "Дата и время", "Фамилия", "Имя", "Отчество", "Дата рождения",
                    "Гражданство", "Паспортные данные", "Направление", "Тип пересечения",
                    "Марка ТС", "Номер ТС", "Цель", "НП Следования", "Оператор"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                int currentRow = 2;

                foreach (var crossing in crossings)
                {
                    var fullNameParts = crossing.FullName.Split(new[] { ' ' }, 3, System.StringSplitOptions.RemoveEmptyEntries);
                    var vehicleParts = crossing.VehicleInfo.Split(new[] { '/' }, 2, System.StringSplitOptions.RemoveEmptyEntries);

                    worksheet.Cell(currentRow, 1).Value = crossing.ID;
                    worksheet.Cell(currentRow, 2).Value = crossing.Timestamp;
                    worksheet.Cell(currentRow, 3).Value = fullNameParts.Length > 0 ? fullNameParts[0] : "";
                    worksheet.Cell(currentRow, 4).Value = fullNameParts.Length > 1 ? fullNameParts[1] : "";
                    worksheet.Cell(currentRow, 5).Value = fullNameParts.Length > 2 ? fullNameParts[2] : "";
                    worksheet.Cell(currentRow, 6).Value = crossing.PersonDob;
                    worksheet.Cell(currentRow, 7).Value = crossing.Citizenship;
                    worksheet.Cell(currentRow, 8).Value = crossing.PersonPassport;
                    worksheet.Cell(currentRow, 9).Value = crossing.Direction;
                    worksheet.Cell(currentRow, 10).Value = crossing.CrossingType;
                    worksheet.Cell(currentRow, 11).Value = vehicleParts.Length > 0 ? vehicleParts[0] : "";
                    worksheet.Cell(currentRow, 12).Value = vehicleParts.Length > 1 ? vehicleParts[1] : "";
                    worksheet.Cell(currentRow, 13).Value = crossing.Purpose;
                    worksheet.Cell(currentRow, 14).Value = crossing.DestinationTown;
                    worksheet.Cell(currentRow, 15).Value = crossing.OperatorUsername;

                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            });
        }
    }
}
