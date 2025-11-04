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
        // --- ИЗМЕНЕНИЕ: Добавлен параметр companions ---
        public static Task ExportPersonReportAsync(
            Person person,
            IEnumerable<Vehicle> vehicles,
            IEnumerable<Crossing> crossings,
            IEnumerable<GoodReportItem> goodsSummary,
            IEnumerable<TravelCompanion> companions,
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

                    document.InsertParagraph("1. Установочные данные")
                        .Bold()
                        .FontSize(14);

                    document.InsertParagraph().Append("Фамилия:").Bold().Append($"\t\t{person.LastName}");
                    document.InsertParagraph().Append("Имя:").Bold().Append($"\t\t\t{person.FirstName}");
                    document.InsertParagraph().Append("Отчество:").Bold().Append($"\t\t\t{person.Patronymic ?? "-"}");
                    document.InsertParagraph().Append("Дата рождения:").Bold().Append($"\t\t{person.Dob}");
                    document.InsertParagraph().Append("Гражданство:").Bold().Append($"\t\t{person.Citizenship}");
                    document.InsertParagraph().Append("Паспортные данные:").Bold().Append($"\t{person.PassportData}");
                    document.InsertParagraph().Append("Дополнительная информация:").Bold().Append($"\t{person.Notes ?? "-"}");

                    document.InsertParagraph();

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

                    if (crossings.Any())
                    {
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
                    }
                    else
                    {
                        document.InsertParagraph("Пересечения не зафиксированы.");
                    }

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

                    document.InsertParagraph();

                    // --- НОВЫЙ РАЗДЕЛ: Таблица совместных поездок ---
                    document.InsertParagraph("5. Совместные поездки (Водители и Пассажиры)")
                        .Bold()
                        .FontSize(14);

                    var companionsList = companions.ToList();
                    if (companionsList.Any())
                    {
                        var companionsTable = document.AddTable(companionsList.Count() + 1, 5);
                        companionsTable.Design = TableDesign.TableGrid;
                        companionsTable.Rows[0].Cells[0].Paragraphs[0].Append("Дата поездки").Bold();
                        companionsTable.Rows[0].Cells[1].Paragraphs[0].Append("ТС").Bold();
                        companionsTable.Rows[0].Cells[2].Paragraphs[0].Append("Роль в поездке").Bold();
                        companionsTable.Rows[0].Cells[3].Paragraphs[0].Append("ФИО").Bold();
                        companionsTable.Rows[0].Cells[4].Paragraphs[0].Append("Дата рождения").Bold();

                        int comp_row = 1;
                        foreach (var companion in companionsList)
                        {
                            companionsTable.Rows[comp_row].Cells[0].Paragraphs[0].Append(companion.Timestamp);
                            companionsTable.Rows[comp_row].Cells[1].Paragraphs[0].Append(companion.VehicleInfo);
                            companionsTable.Rows[comp_row].Cells[2].Paragraphs[0].Append(companion.Role);
                            companionsTable.Rows[comp_row].Cells[3].Paragraphs[0].Append(companion.FullName);
                            companionsTable.Rows[comp_row].Cells[4].Paragraphs[0].Append(companion.Dob);
                            comp_row++;
                        }
                        document.InsertTable(companionsTable);
                    }
                    else
                    {
                        document.InsertParagraph("Совместных поездок с другими лицами не зафиксировано.");
                    }

                    document.InsertParagraph();

                    // --- НОВЫЙ РАЗДЕЛ: Список связанных лиц ---
                    document.InsertParagraph("6. Связанные лица (сводка)")
                        .Bold()
                        .FontSize(14);

                    var associatedPersons = companionsList
                        .GroupBy(c => new { c.FullName, c.Dob })
                        .Select(g => g.Key)
                        .OrderBy(p => p.FullName)
                        .ToList();

                    if (associatedPersons.Any())
                    {
                        foreach (var associatedPerson in associatedPersons)
                        {
                            document.InsertParagraph($"\u2022 {associatedPerson.FullName} ({associatedPerson.Dob})");
                        }
                    }
                    else
                    {
                        document.InsertParagraph("Связанных лиц не найдено.");
                    }


                    document.Save();
                }
            });
        }
    }
}

