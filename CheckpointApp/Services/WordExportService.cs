using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CheckpointApp.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace CheckpointApp.Services
{
    public class WordExportService
    {
        public Task ExportPersonReportAsync(
            Person person,
            IEnumerable<Vehicle> vehicles,
            IEnumerable<Crossing> crossings,
            IEnumerable<GoodReportItem> goodsSummary,
            string filePath)
        {
            return Task.Run(() =>
            {
                using (var document = DocX.Create(filePath))
                {
                    document.InsertParagraph("ЗАПРОС НА ЛИЦО")
                        .Bold()
                        .FontSize(16)
                        .Alignment = Alignment.center;

                    document.InsertParagraph($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm:ss}")
                        .FontSize(10)
                        .Alignment = Alignment.center;

                    document.InsertParagraph();

                    // --- ИЗМЕНЕНИЕ 1: Замена таблицы на простой текст ---
                    document.InsertParagraph("1. Установочные данные")
                        .Bold()
                        .FontSize(14);

                    // Используем параграфы вместо таблицы для лучшего форматирования
                    document.InsertParagraph().Append("Фамилия:").Bold().Append($"\t\t{person.LastName}");
                    document.InsertParagraph().Append("Имя:").Bold().Append($"\t\t\t{person.FirstName}");
                    document.InsertParagraph().Append("Отчество:").Bold().Append($"\t\t\t{person.Patronymic ?? "-"}");
                    document.InsertParagraph().Append("Дата рождения:").Bold().Append($"\t\t{person.Dob}");
                    document.InsertParagraph().Append("Гражданство:").Bold().Append($"\t\t{person.Citizenship}");
                    document.InsertParagraph().Append("Паспортные данные:").Bold().Append($"\t{person.PassportData}");
                    document.InsertParagraph().Append("Дополнительная информация:").Bold().Append($"\t{person.Notes ?? "-"}");

                    document.InsertParagraph();
                    // --- КОНЕЦ ИЗМЕНЕНИЯ 1 ---

                    document.InsertParagraph("2. Использованные транспортные средства")
                        .Bold()
                        .FontSize(14);

                    if (vehicles.Any())
                    {
                        var vehiclesTable = document.AddTable(vehicles.Count() + 1, 2);
                        vehiclesTable.Design = TableDesign.TableGrid;
                        vehiclesTable.Rows[0].Cells[0].Paragraphs[0].Append("Марка").Bold();
                        vehiclesTable.Rows[0].Cells[1].Paragraphs[0].Append("Гос. номер").Bold();

                        int v_row = 1;
                        foreach (var vehicle in vehicles)
                        {
                            vehiclesTable.Rows[v_row].Cells[0].Paragraphs[0].Append(vehicle.Make);
                            vehiclesTable.Rows[v_row].Cells[1].Paragraphs[0].Append(vehicle.LicensePlate);
                            v_row++;
                        }
                        document.InsertTable(vehiclesTable);
                    }
                    else
                    {
                        document.InsertParagraph("Транспортные средства не использовались.");
                    }
                    document.InsertParagraph();

                    document.InsertParagraph("3. История пересечений")
                        .Bold()
                        .FontSize(14);

                    var crossingsTable = document.AddTable(crossings.Count() + 1, 5);
                    crossingsTable.Design = TableDesign.TableGrid;
                    crossingsTable.Rows[0].Cells[0].Paragraphs[0].Append("Дата и время").Bold();
                    crossingsTable.Rows[0].Cells[1].Paragraphs[0].Append("Направление").Bold();
                    crossingsTable.Rows[0].Cells[2].Paragraphs[0].Append("Тип").Bold();
                    crossingsTable.Rows[0].Cells[3].Paragraphs[0].Append("Цель").Bold();
                    crossingsTable.Rows[0].Cells[4].Paragraphs[0].Append("НП Следования").Bold();

                    int c_row = 1;
                    foreach (var crossing in crossings)
                    {
                        crossingsTable.Rows[c_row].Cells[0].Paragraphs[0].Append(crossing.Timestamp);
                        crossingsTable.Rows[c_row].Cells[1].Paragraphs[0].Append(crossing.Direction);
                        crossingsTable.Rows[c_row].Cells[2].Paragraphs[0].Append(crossing.CrossingType);
                        crossingsTable.Rows[c_row].Cells[3].Paragraphs[0].Append(crossing.Purpose ?? "-");
                        crossingsTable.Rows[c_row].Cells[4].Paragraphs[0].Append(crossing.DestinationTown ?? "-");
                        c_row++;
                    }
                    document.InsertTable(crossingsTable);
                    document.InsertParagraph();

                    document.InsertParagraph("4. Сводка по перемещенным товарам и грузам")
                        .Bold()
                        .FontSize(14);

                    if (goodsSummary.Any())
                    {
                        var goodsTable = document.AddTable(goodsSummary.Count() + 1, 3);
                        goodsTable.Design = TableDesign.TableGrid;
                        goodsTable.Rows[0].Cells[0].Paragraphs[0].Append("Наименование").Bold();
                        goodsTable.Rows[0].Cells[1].Paragraphs[0].Append("Общее количество").Bold();
                        goodsTable.Rows[0].Cells[2].Paragraphs[0].Append("Ед. изм.").Bold();

                        int g_row = 1;
                        foreach (var good in goodsSummary)
                        {
                            goodsTable.Rows[g_row].Cells[0].Paragraphs[0].Append(good.Description);
                            goodsTable.Rows[g_row].Cells[1].Paragraphs[0].Append(good.TotalQuantity.ToString());
                            goodsTable.Rows[g_row].Cells[2].Paragraphs[0].Append(good.Unit);
                            g_row++;
                        }
                        document.InsertTable(goodsTable);
                    }
                    else
                    {
                        document.InsertParagraph("Товары и грузы не перемещались.");
                    }

                    document.Save();
                }
            });
        }
    }
}
