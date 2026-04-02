using Data;
using Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services.Maps;

namespace Services.Seeding
{
    public class DataSeeder : IDataSeeder
    {
        private const string DefaultPassword = "Test123!";
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        private static readonly string[] Sections = ["a", "b", "v", "g"];

        private static readonly string[] StudentFirstNames =
        [
            "ivan", "maria", "georgi", "elena", "nikolay", "teodora", "petar", "viktoria",
            "kristian", "yoana", "stefan", "dariya", "martin", "desislava", "boris", "anna",
            "kalin", "simona", "atanas", "rada", "milen", "petya", "denislav", "plamena"
        ];

        private static readonly string[] StudentLastNames =
        [
            "petrov", "ivanov", "georgiev", "dimitrov", "nikolov", "stoyanov", "vasilev", "hristov",
            "marinov", "kolev", "angelov", "kirilov", "tsvetkov", "todorov", "donev", "popov"
        ];

        private static readonly TeacherSeed[] TeacherSeeds =
        [
            new("teacher", "teacher@mg-akp.bg"),
            new("m.ivanova", "m.ivanova@mg-akp.bg"),
            new("n.kolev", "n.kolev@mg-akp.bg"),
            new("g.stoyanova", "g.stoyanova@mg-akp.bg"),
            new("r.petrova", "r.petrova@mg-akp.bg")
        ];

        private static readonly ThreadSeed[] ThreadSeeds =
        [
            new("[News] График за пробните изпити по математика", "Пробни изпити", "Публикуваме графика за пробните изпити по математика в МГ \"Академик Кирил Попов\". В тази тема ще допълваме информация при промени.", Role.Admin, 5, true, false, 4),
            new("[News] Ден на отворените врати в МГ \"Академик Кирил Попов\"", "Ден на отворените врати", "На 12 април училището ще посрещне гости и бъдещи ученици. Тук ще събираме въпроси и организационни уточнения.", Role.Teacher, 14, true, false, 5),
            new("[News] Временна промяна на входа към голямата сграда", "Временна промяна на входа", "Северният вход ще се използва с ограничен достъп заради ремонт на настилката. Следете темата за актуални указания.", Role.Admin, 8, true, true, 3),
            new("[News] Резултати от вътрешното състезание по информатика", "Резултати от състезанието", "Поздравления на всички участници в училищното състезание по информатика. Публикуваме резултатите и следващите стъпки за подготовка.", Role.Teacher, 21, false, false, 4),
            new("Осветлението в коридора до 203 примигва", "Осветление до 203", "В коридора до 203 лампите примигват още от миналата седмица и това затруднява часовете в късния следобед.", Role.Student, 3, false, false, 6),
            new("Сутрешно струпване пред лобито", "Струпване пред лобито", "Между 7:35 и 7:50 пред лобито става твърде тясно и се получават сериозни струпвания. Нека съберем идеи за по-добра организация.", Role.Student, 2, false, false, 7),
            new("Нужда от нови столове в 315", "Оборудване в 315", "В 315 има няколко стола с разхлабени облегалки. Добре е да ги опишем и да подадем конкретна заявка.", Role.Student, 18, false, false, 5),
            new("Състояние на голямото игрище след дъжд", "Игрището след дъжд", "След последния дъжд има локви и хлъзгави участъци около червеното игрище. Споделете къде проблемът е най-осезаем.", Role.Teacher, 4, false, false, 5),
            new("Поддръжка на санитарните помещения на 2 етаж", "Санитарни помещения 2 етаж", "В санитарните помещения на втория етаж често липсват консумативи и има проблеми с част от мивките.", Role.Student, 11, false, false, 6),
            new("Компютрите в лабораторията по програмиране се рестартират", "Лаборатория програмиране", "Няколко машини в лабораторията по програмиране се рестартират при по-тежки задачи. Нека съберем кои точно работни места са засегнати.", Role.Teacher, 7, false, false, 5),
            new("Организация на клуб по роботика в малката сграда", "Клуб по роботика", "Има интерес за регулярни срещи на клуба по роботика след часовете. Тази тема е за идеи относно график и свободна зала.", Role.Teacher, 26, false, false, 4),
            new("Шум в библиотеката след шести час", "Шум в библиотеката", "След шести час библиотеката често се ползва и за разговори, което пречи на учениците, които се подготвят за олимпиади.", Role.Student, 17, false, false, 5),
            new("Почистване около столовата и алеята към двора", "Почистване около столовата", "Около столовата и алеята към двора остават отпадъци след голямото междучасие. Тук можем да описваме най-проблемните моменти.", Role.Student, 6, false, false, 5),
            new("Температурата в 113 е ниска сутрин", "Температура в 113", "В първите два часа в 113 е осезаемо студено и стаята се затопля твърде бавно.", Role.Student, 23, false, false, 4),
            new("Счупен контакт до 101", "Контакт до 101", "До 101 има контакт с разхлабен панел. По-добре е да го опишем и да спрем използването му, докато бъде обезопасен.", Role.Student, 1, false, true, 4),
            new("Идеи за ученически инициативи през пролетта", "Пролетни инициативи", "Да съберем предложения за смислени инициативи през април и май - работилници, състезания и доброволчески кампании.", Role.Student, 12, false, false, 8),
            new("Предложение за шах турнир във фоайето", "Шах турнир", "Има интерес към вътрешноучилищен шах турнир. Нека обсъдим подходящ формат и място.", Role.Student, 28, false, false, 4),
            new("Нужда от повече кошчета по северната алея", "Кошчета по северната алея", "По северната алея кошчетата не са достатъчни в натоварените часове. Добре е да отбележим в кои точки се натрупват отпадъци.", Role.Student, 9, false, false, 4),
            new("Състояние на съблекалните до ФВС салон 1", "Съблекални до ФВС 1", "Съблекалните до салон 1 имат нужда от по-редовен оглед и по-добра вентилация.", Role.Teacher, 15, false, false, 5),
            new("Забавяне при достъпа до профили в компютърния кабинет", "Достъп до профили", "При началото на часа част от профилите в компютърния кабинет се зареждат бавно и се губи време.", Role.Student, 10, false, false, 5),
            new("Подобрения в двора до малката сграда", "Двор до малката сграда", "Зоната между малката сграда и игрището има потенциал за по-приятно използване в междучасията. Какво според вас е най-необходимо?", Role.Student, 19, false, false, 6),
            new("Липсват указателни табели към четвъртия етаж", "Указателни табели", "Гостите и новите ученици често се затрудняват при ориентацията към четвъртия етаж. Нека опишем къде табелите биха помогнали най-много.", Role.Teacher, 30, false, false, 3),
            new("Нова тиха зона за самоподготовка", "Тиха зона", "Да обсъдим къде в училището можем да оформим тиха зона за самоподготовка след часовете.", Role.Student, 13, false, false, 7),
            new("Състояние на мивките в лабораторията по химия", "Мивки в химия", "В лабораторията по химия две от мивките текат и трябва да се опишат, за да се подаде навременен сигнал.", Role.Teacher, 16, false, false, 4)
        ];
        private static readonly PinLocationSeed[] PinLocationSeeds =
        [
            new("campus", 410, 210, "Северна алея", "Организация", 6,
                ["Сутрешно струпване на северната алея", "Тясно преминаване пред входа", "Трудно разминаване пред северния вход"],
                ["Потокът от ученици към голямата сграда е най-силен между 7:35 и 7:50.", "При дъждовно време зоната се натоварва и придвижването се забавя.", "Добре е да има по-ясна организация на влизащите потоци."]),
            new("campus", 655, 255, "Червено игрище", "Спортна база", 7,
                ["Настилката на игрището е хлъзгава", "Мрежата на игрището има нужда от оглед", "Събират се локви до игрището"],
                ["След последния дъжд по периферията остават мокри и хлъзгави участъци.", "Огражденията около игрището трябва да бъдат проверени.", "Тренировките се затрудняват при мокра настилка и липса на оттичане."]),
            new("campus", 690, 600, "Югоизточна зона на двора", "Поддръжка", 4,
                ["Настилката до двора се рони", "Има неравен участък до алеята", "Нужен е оглед на асфалта"],
                ["При натоварване участъкът се усеща неравен и има риск от подхлъзване.", "Добре е зоната да се огледа преди пролетните събития на открито.", "Проблемът се забелязва най-много в края на деня."]),
            new("main:1", 342, 292, "Лоби", "Организация", 8,
                ["Струпване пред лобито", "Хлъзгав участък в лобито", "Изчакване пред информационното табло"],
                ["Преминаването пред лобито се затруднява в голямото междучасие.", "При мокро време на входа към лобито се събира влага.", "Информационното табло събира много ученици непосредствено пред стълбите."]),
            new("main:1", 404, 205, "ФВС салон 1", "Спортна база", 4,
                ["Съблекалните до салон 1 имат нужда от оглед", "Пейките в съблекалните са разхлабени", "Проветрението в спортната зона е слабо"],
                ["Част от пейките са нестабилни и трябва да бъдат прегледани.", "След часовете миризмата остава задържана твърде дълго.", "Спортната зона е натоварена и има нужда от по-честа поддръжка."]),
            new("main:1", 420, 655, "Столова", "Хигиена", 6,
                ["Зоната около столовата се нуждае от по-често почистване", "Опашката пред столовата блокира алеята", "Липсва консуматив до мивките на столовата"],
                ["След голямото междучасие около столовата остават отпадъци.", "Потокът от ученици пред столовата затруднява изхода към двора.", "Мивките до столовата не винаги са добре заредени."]),
            new("main:1", 188, 286, "Стълбище етаж 1", "Безопасност", 5,
                ["Хлъзгаво при стълбището на първи етаж", "Парапетът при стълбището скърца", "Струпване пред стълбището"],
                ["Стълбищната зона е силно натоварена и има риск при мокро време.", "Парапетът се усеща разхлабен в горната част.", "При смяна на часове преминаването се стеснява."]),
            new("main:2", 348, 318, "Коридор втори етаж", "Поддръжка", 7,
                ["Осветлението в коридора до 203 примигва", "Плочка в коридора е разлепена", "Шумна врата по коридора"],
                ["Осветлението отслабва в късните часове и прави зоната по-тъмна.", "Има участък от настилката, който се усеща неравен.", "Една от вратите хлопа силно при течение."]),
            new("main:2", 190, 676, "Стълбище етаж 2", "Безопасност", 4,
                ["Задръстване при стълбището на втори етаж", "Нужна е по-ясна маркировка към стълбите", "Подът до стълбите е хлъзгав"],
                ["Между втори и трети етаж движението се събира в тесен поток.", "Гостите често се лутат около стълбищната клетка.", "При чистене подът остава мокър твърде дълго."]),
            new("main:3", 124, 160, "Библиотека", "Организация", 3,
                ["Шум около библиотеката след шести час", "Недостиг на места за самоподготовка", "Входът към библиотеката се задръства"],
                ["В края на деня се събират едновременно ученици за връщане и заемане на книги.", "Най-тихите места се запълват бързо при подготовка за олимпиади.", "При смесване на потоците се губи спокойствието на зоната."]),
            new("main:4", 124, 160, "Лаборатория програмиране", "Оборудване", 5,
                ["Компютър в лабораторията се рестартира", "Клавиатури в лабораторията имат нужда от подмяна", "Мрежата в кабинета забавя влизането"],
                ["Няколко работни места прекъсват при по-натоварени задачи.", "Част от периферията е износена и създава затруднения в часовете.", "При едновременно логване на целия клас мрежата се усеща бавна."]),
            new("small:1", 315, 170, "Лаборатория физика", "Оборудване", 4,
                ["Лабораторно оборудване във физика има нужда от оглед", "Плот в лабораторията се клати", "Нужна е подмяна на част от кабелите"],
                ["Част от оборудването трябва да бъде прегледано преди следващия демонстрационен час.", "Един от плотовете е нестабилен при работа в екип.", "Кабелите около работните места трябва да се подредят по-безопасно."]),
            new("small:1", 535, 170, "Стая 113", "Поддръжка", 3,
                ["В 113 е студено сутрин", "Прозорецът в 113 не уплътнява добре", "Шум от коридора се чува силно в 113"],
                ["Температурата е ниска през първите два часа.", "При вятър се усеща течение до прозорците.", "Звукът от коридора влиза силно по време на контролни."]),
            new("small:2", 314, 170, "Стая 211", "Поддръжка", 3,
                ["Врата на 211 се затваря трудно", "Осветлението в 211 е слабо", "Дъската в 211 е надраскана"],
                ["При смяна на часа вратата остава полуотворена.", "В задната част на стаята осветлението не е равномерно.", "Част от повърхността на дъската вече не се почиства добре."]),
            new("small:2", 535, 170, "Стая 213", "Оборудване", 3,
                ["Столовете в 213 са разклатени", "Проекторът в 213 губи фокус", "Контакт до 213 трябва да се обезопаси"],
                ["Има няколко стола с разхлабени винтове.", "Проекторът се нуждае от калибрация и оглед.", "Контактът не трябва да се използва, докато не се прегледа."]),
            new("small:3", 315, 180, "Технологии и иновации", "Оборудване", 5,
                ["Оборудване в STEM зоната чака настройка", "Работна маса в STEM кабинета е нестабилна", "Нужни са нови разклонители в технологичната зона"],
                ["Част от комплектите за демонстрации стоят неизползваеми без кратка поддръжка.", "Една от работните маси се клати при групова работа.", "Разклонителите не достигат при едновременна работа на няколко отбора."]),
            new("small:3", 535, 498, "Стълбище малка сграда", "Безопасност", 4,
                ["Стълбището в малката сграда е натоварено", "Парапетът в малката сграда има нужда от преглед", "Подът при стълбите се мокри"],
                ["При приключване на часовете потокът към изхода се събира на едно място.", "Парапетът трябва да бъде огледан преди натоварения период с клубни дейности.", "След почистване зоната остава хлъзгава за дълго."])
        ];

        private static readonly string[] StudentReplyFragments =
        [
            "И при нашия клас се усеща същият проблем.",
            "Подкрепям да го отбележим по-подробно и с конкретни часове.",
            "Вижда се най-много в натоварените междучасия.",
            "Мога да потвърдя, че това се случва почти всеки ден.",
            "Добре е да има и по-ясна организация от наша страна.",
            "Според мен проблемът е най-силен в края на учебния ден."
        ];

        private static readonly string[] TeacherReplyFragments =
        [
            "Ще го включа в седмичния списък за проверка.",
            "Полезно е, че описвате и точната зона.",
            "Нека до края на деня съберем още конкретни наблюдения.",
            "Ще го координирам с ръководството и поддръжката.",
            "Ако има ново развитие, ще го отбележа тук."
        ];

        private static readonly string[] AdminReplyFragments =
        [
            "Сигналът е видян и ще бъде включен в следващия преглед.",
            "Ще проследим казуса и ще върнем обратна връзка в темата.",
            "Моля описвайте и кога проблемът е най-осезаем.",
            "Темата е полезна и ще я оставим активна, докато съберем решение."
        ];

        private static readonly string[] ReportReasons =
        [
            "Груб тон в дискусията",
            "Неподходящо съдържание",
            "Спам или дублиране",
            "Лични нападки",
            "Подвеждаща информация"
        ];

        private static readonly string[] TeacherRequestMotivations =
        [
            "Искам да публикувам официални съобщения към учениците и да помагам при модерацията на темите.",
            "Необходимо ми е учителско ниво на достъп за клубните дейности и новините към учениците.",
            "Ще използвам профила за обратна връзка по състезания, кабинети и STEM инициативи.",
            "Имам нужда от достъп за официални новини и координация около събитията на гимназията."
        ];

        public DataSeeder(AppDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        public async Task SeedAsync()
        {
            await ClearAllAsync();

            var users = CreateUsers();
            _db.Users.AddRange(users);
            await _db.SaveChangesAsync();

            var threads = CreateThreads(users);
            _db.ForumThreads.AddRange(threads);
            await _db.SaveChangesAsync();

            var posts = CreatePosts(users, threads);
            _db.ForumPosts.AddRange(posts);
            await _db.SaveChangesAsync();

            foreach (var thread in threads)
            {
                thread.LastPostAt = posts
                    .Where(post => post.Thread.Id == thread.Id)
                    .OrderByDescending(post => post.CreatedAt)
                    .Select(post => (DateTime?)post.CreatedAt)
                    .FirstOrDefault() ?? thread.CreatedAt;
            }

            var pins = CreatePins(users);
            _db.EventPins.AddRange(pins);
            await _db.SaveChangesAsync();

            _db.PostVotes.AddRange(CreatePostVotes(users, posts));
            _db.PinVotes.AddRange(CreatePinVotes(users, pins));
            _db.Reports.AddRange(CreateReports(users, posts, threads, pins));
            _db.PasswordResetTokens.AddRange(CreatePasswordResetTokens(users));
            _db.TeacherRegistrationRequests.AddRange(CreateTeacherRegistrationRequests(users));

            await _db.SaveChangesAsync();
        }
        private async Task ClearAllAsync()
        {
            _db.PostVotes.RemoveRange(_db.PostVotes);
            _db.PinVotes.RemoveRange(_db.PinVotes);
            _db.Reports.RemoveRange(_db.Reports);
            _db.PasswordResetTokens.RemoveRange(_db.PasswordResetTokens);
            _db.ForumPosts.RemoveRange(_db.ForumPosts);
            _db.ForumThreads.RemoveRange(_db.ForumThreads);
            _db.EventPins.RemoveRange(_db.EventPins);
            _db.TeacherRegistrationRequests.RemoveRange(_db.TeacherRegistrationRequests);
            _db.Users.RemoveRange(_db.Users.IgnoreQueryFilters());
            await _db.SaveChangesAsync();
            await ResetIdentityCountersAsync();
        }

        private async Task ResetIdentityCountersAsync()
        {
            var tables = new[]
            {
                "PostVotes",
                "PinVotes",
                "Reports",
                "PasswordResetTokens",
                "ForumPosts",
                "ForumThreads",
                "EventPins",
                "TeacherRegistrationRequests",
                "Users"
            };

            foreach (var table in tables)
            {
                await _db.Database.ExecuteSqlRawAsync($"DBCC CHECKIDENT ('[{table}]', RESEED, 0);");
            }
        }

        private List<User> CreateUsers()
        {
            var users = new List<User>
            {
                CreateUser("admin", "admin@mg-akp.bg", Role.Admin)
            };

            users.AddRange(TeacherSeeds.Select(seed => CreateUser(seed.Username, seed.Email, Role.Teacher)));

            for (var i = 0; i < 44; i++)
            {
                var gradeLevel = 5 + (i % 8);
                var section = Sections[i % Sections.Length];
                var first = StudentFirstNames[i % StudentFirstNames.Length];
                var last = StudentLastNames[(i * 3) % StudentLastNames.Length];
                var suffix = (i / Sections.Length) + 1;
                var username = $"{first}.{last}.{gradeLevel}{section}{suffix}";
                var student = CreateUser(username, $"{username}@mg-akp.bg", Role.Student, gradeLevel);

                if (i == 6)
                {
                    student.IsBanned = true;
                    student.BannedUntil = DateTime.UtcNow.AddDays(4);
                    student.BanReason = "Временно ограничение заради системни нарушения на правилата.";
                }

                if (i == 17)
                {
                    student.IsBanned = true;
                    student.BannedUntil = null;
                    student.BanReason = "Блокиран до допълнителен преглед от администратор.";
                }

                users.Add(student);
            }

            return users;
        }

        private List<ForumThread> CreateThreads(List<User> users)
        {
            var admins = users.Where(user => user.Role == Role.Admin).ToList();
            var teachers = users.Where(user => user.Role == Role.Teacher).ToList();
            var students = users.Where(user => user.Role == Role.Student).ToList();
            var threads = new List<ForumThread>(ThreadSeeds.Length);

            for (var i = 0; i < ThreadSeeds.Length; i++)
            {
                var seed = ThreadSeeds[i];
                var creator = seed.CreatorRole switch
                {
                    Role.Admin => admins[i % admins.Count],
                    Role.Teacher => teachers[i % teachers.Count],
                    _ => students[i % students.Count]
                };

                threads.Add(new ForumThread
                {
                    Title = seed.Title,
                    CreatedByUser = creator,
                    CreatedAt = DateTime.UtcNow.AddDays(-seed.DaysAgo).AddMinutes(-(i * 13) % 59),
                    IsPinned = seed.IsPinned,
                    IsLocked = seed.IsLocked
                });
            }

            return threads;
        }

        private List<ForumPost> CreatePosts(List<User> users, List<ForumThread> threads)
        {
            var admins = users.Where(user => user.Role == Role.Admin).ToList();
            var teachers = users.Where(user => user.Role == Role.Teacher).ToList();
            var students = users.Where(user => user.Role == Role.Student).ToList();
            var posts = new List<ForumPost>();

            for (var threadIndex = 0; threadIndex < threads.Count; threadIndex++)
            {
                var thread = threads[threadIndex];
                var seed = ThreadSeeds[threadIndex];
                var createdPosts = new List<ForumPost>();

                var rootPost = new ForumPost
                {
                    Title = seed.RootTitle,
                    Content = seed.RootContent,
                    Thread = thread,
                    User = thread.CreatedByUser,
                    CreatedAt = thread.CreatedAt.AddMinutes(25),
                    IsDeleted = false
                };

                posts.Add(rootPost);
                createdPosts.Add(rootPost);

                for (var replyIndex = 0; replyIndex < seed.ReplyCount; replyIndex++)
                {
                    var author = SelectReplyAuthor(students, teachers, admins, seed, threadIndex, replyIndex);
                    var parent = replyIndex >= 2 && replyIndex % 3 == 0
                        ? createdPosts[Math.Max(0, createdPosts.Count - 2)]
                        : rootPost;

                    var reply = new ForumPost
                    {
                        Title = replyIndex % 4 == 0 ? $"Отговор {replyIndex + 1}" : null,
                        Content = BuildReplyContent(seed, author.Role, replyIndex),
                        Thread = thread,
                        User = author,
                        ParentPost = ReferenceEquals(parent, rootPost) ? null : parent,
                        CreatedAt = rootPost.CreatedAt.AddHours(3 + replyIndex * 4).AddMinutes((threadIndex * 17 + replyIndex * 9) % 50),
                        IsDeleted = false
                    };

                    posts.Add(reply);
                    createdPosts.Add(reply);
                }
            }

            return posts;
        }
        private List<EventPin> CreatePins(List<User> users)
        {
            var authors = users.Where(user => user.Role != Role.Admin || user.Username == "admin").ToList();
            var pins = new List<EventPin>();
            var globalIndex = 0;

            foreach (var location in PinLocationSeeds)
            {
                for (var i = 0; i < location.Count; i++)
                {
                    var encoded = IndoorMapGeometry.EncodeLayerPoint(location.LayerId, location.X, location.Y);
                    var title = location.Titles[i % location.Titles.Length];
                    var description = location.Descriptions[(i + globalIndex) % location.Descriptions.Length];

                    pins.Add(new EventPin
                    {
                        Title = title,
                        Description = $"{description} Зона: {location.ZoneLabel}.",
                        Category = location.Category,
                        Latitude = encoded.Latitude,
                        Longitude = encoded.Longitude,
                        CreatedByUser = authors[(globalIndex * 5 + i * 3) % authors.Count],
                        CreatedAt = DateTime.UtcNow
                            .AddDays(-(globalIndex % 36))
                            .AddHours(-((i * 3 + globalIndex) % 18))
                            .AddMinutes(-((globalIndex * 11) % 45))
                    });

                    globalIndex++;
                }
            }

            return pins.OrderByDescending(pin => pin.CreatedAt).ToList();
        }

        private List<PostVote> CreatePostVotes(List<User> users, List<ForumPost> posts)
        {
            var votes = new List<PostVote>();
            var used = new HashSet<string>();
            var orderedPosts = posts.OrderByDescending(post => post.CreatedAt).ToList();

            for (var userIndex = 0; userIndex < users.Count; userIndex++)
            {
                var user = users[userIndex];
                var interactions = user.Role == Role.Student ? 5 + (userIndex % 3) : 8 + (userIndex % 2);
                var poolSize = Math.Min(orderedPosts.Count, 26 + (userIndex % 12));

                for (var step = 0; step < interactions; step++)
                {
                    var post = orderedPosts[(userIndex * 7 + step * 11) % poolSize];
                    if (post.User.Id == user.Id)
                    {
                        continue;
                    }

                    var key = $"{user.Id}:{post.Id}";
                    if (!used.Add(key))
                    {
                        continue;
                    }

                    votes.Add(new PostVote
                    {
                        User = user,
                        Post = post,
                        Value = (step + userIndex) % 9 == 0 ? VoteValue.Down : VoteValue.Up
                    });
                }
            }

            return votes;
        }

        private List<PinVote> CreatePinVotes(List<User> users, List<EventPin> pins)
        {
            var votes = new List<PinVote>();
            var used = new HashSet<string>();
            var orderedPins = pins.OrderByDescending(pin => pin.CreatedAt).ToList();
            var boostedPins = orderedPins.Take(14).ToList();

            for (var userIndex = 0; userIndex < users.Count; userIndex++)
            {
                var user = users[userIndex];
                var interactions = user.Role == Role.Student ? 6 + (userIndex % 3) : 10 + (userIndex % 3);
                var poolSize = Math.Min(orderedPins.Count, 22 + (userIndex % 10));

                for (var step = 0; step < interactions; step++)
                {
                    var pin = orderedPins[(userIndex * 5 + step * 7) % poolSize];
                    if (pin.CreatedByUser.Id == user.Id)
                    {
                        continue;
                    }

                    var key = $"{user.Id}:{pin.Id}";
                    if (!used.Add(key))
                    {
                        continue;
                    }

                    votes.Add(new PinVote
                    {
                        User = user,
                        Pin = pin,
                        Value = (step + userIndex) % 11 == 0 ? VoteValue.Down : VoteValue.Up
                    });
                }
            }

            foreach (var user in users.Where(user => user.Role != Role.Student).Take(6))
            {
                foreach (var pin in boostedPins)
                {
                    var key = $"{user.Id}:{pin.Id}";
                    if (!used.Add(key) || pin.CreatedByUser.Id == user.Id)
                    {
                        continue;
                    }

                    votes.Add(new PinVote
                    {
                        User = user,
                        Pin = pin,
                        Value = VoteValue.Up
                    });
                }
            }

            return votes;
        }
        private List<Report> CreateReports(List<User> users, List<ForumPost> posts, List<ForumThread> threads, List<EventPin> pins)
        {
            var admin = users.First(user => user.Role == Role.Admin);
            var teachers = users.Where(user => user.Role == Role.Teacher).ToList();
            var students = users.Where(user => user.Role == Role.Student).ToList();
            var reports = new List<Report>();

            for (var i = 0; i < 18; i++)
            {
                var targetType = (ReportTargetType)(i % 4);
                var status = (i % 5) switch
                {
                    0 => ReportStatus.Actioned,
                    1 => ReportStatus.Reviewed,
                    2 => ReportStatus.Dismissed,
                    _ => ReportStatus.Open
                };

                var report = new Report
                {
                    Reporter = students[(i * 2) % students.Count],
                    TargetType = targetType,
                    TargetId = targetType switch
                    {
                        ReportTargetType.Post => posts[(i * 7) % posts.Count].Id,
                        ReportTargetType.Thread => threads[(i * 5) % threads.Count].Id,
                        ReportTargetType.Pin => pins[(i * 9) % pins.Count].Id,
                        _ => students[(i * 3) % students.Count].Id
                    },
                    Reason = ReportReasons[i % ReportReasons.Length],
                    Details = $"Автоматично генериран примерен сигнал #{i + 1} за реалистична активност в платформата.",
                    Status = status,
                    CreatedAt = DateTime.UtcNow.AddDays(-(i % 20)).AddHours(-(i * 2))
                };

                if (status != ReportStatus.Open)
                {
                    report.ResolvedAt = report.CreatedAt.AddHours(10 + i);
                    report.ResolvedBy = i % 2 == 0 ? admin : teachers[i % teachers.Count];
                }

                reports.Add(report);
            }

            return reports;
        }

        private List<PasswordResetToken> CreatePasswordResetTokens(List<User> users)
        {
            var tokens = new List<PasswordResetToken>();
            for (var i = 0; i < 12; i++)
            {
                tokens.Add(new PasswordResetToken
                {
                    User = users[(i * 4) % users.Count],
                    TokenHash = Guid.NewGuid().ToString("N"),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30 + i * 10),
                    IsUsed = i % 4 == 0,
                    CreatedAt = DateTime.UtcNow.AddHours(-(i + 1))
                });
            }

            return tokens;
        }

        private List<TeacherRegistrationRequest> CreateTeacherRegistrationRequests(List<User> users)
        {
            var reviewers = users.Where(user => user.Role != Role.Student).ToList();
            var requests = new List<TeacherRegistrationRequest>();

            for (var i = 0; i < 6; i++)
            {
                var status = (i % 3) switch
                {
                    0 => TeacherRegistrationStatus.Pending,
                    1 => TeacherRegistrationStatus.Approved,
                    _ => TeacherRegistrationStatus.Rejected
                };

                requests.Add(new TeacherRegistrationRequest
                {
                    Username = $"teacher-candidate-{i + 1:00}",
                    Email = $"teacher-candidate-{i + 1:00}@mg-akp.bg",
                    PasswordHash = Guid.NewGuid().ToString("N"),
                    Motivation = TeacherRequestMotivations[i % TeacherRequestMotivations.Length],
                    Status = status,
                    CreatedAt = DateTime.UtcNow.AddDays(-(i * 4 + 2)),
                    ReviewedAt = status == TeacherRegistrationStatus.Pending ? null : DateTime.UtcNow.AddDays(-(i * 4)),
                    ReviewNote = status switch
                    {
                        TeacherRegistrationStatus.Approved => "Одобрена примерна заявка за учителски достъп.",
                        TeacherRegistrationStatus.Rejected => "Отказана примерна заявка след вътрешен преглед.",
                        _ => null
                    },
                    ReviewedBy = status == TeacherRegistrationStatus.Pending ? null : reviewers[i % reviewers.Count]
                });
            }

            return requests;
        }

        private User CreateUser(string username, string email, Role role, int? gradeLevel = null)
        {
            var schoolYearStart = gradeLevel.HasValue ? DetermineSchoolYearStart(DateTime.UtcNow) : (int?)null;
            var user = new User
            {
                Username = username,
                Email = email,
                Role = role,
                IsDeleted = false,
                DeletedAt = null,
                IsBanned = false,
                BannedUntil = null,
                BanReason = null,
                PhotoUrl = null,
                GradeLevel = gradeLevel,
                SchoolYearStart = schoolYearStart,
                ScheduledDeletionAt = gradeLevel.HasValue && schoolYearStart.HasValue
                    ? CalculateScheduledDeletionUtc(gradeLevel.Value, schoolYearStart.Value)
                    : null
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, DefaultPassword);
            return user;
        }

        private static int DetermineSchoolYearStart(DateTime referenceUtc)
        {
            var boundary = new DateTime(referenceUtc.Year, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            return referenceUtc >= boundary ? referenceUtc.Year : referenceUtc.Year - 1;
        }

        private static DateTime CalculateScheduledDeletionUtc(int gradeLevel, int schoolYearStart)
        {
            var completionYear = schoolYearStart + 1;
            var completionDate = gradeLevel switch
            {
                12 => new DateTime(completionYear, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                <= 3 => new DateTime(completionYear, 5, 29, 0, 0, 0, DateTimeKind.Utc),
                <= 6 => new DateTime(completionYear, 6, 12, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(completionYear, 6, 30, 0, 0, 0, DateTimeKind.Utc)
            };

            return completionDate.AddDays(1);
        }

        private static User SelectReplyAuthor(List<User> students, List<User> teachers, List<User> admins, ThreadSeed seed, int threadIndex, int replyIndex)
        {
            if (seed.Title.StartsWith("[News]", StringComparison.OrdinalIgnoreCase))
            {
                return replyIndex % 4 == 3
                    ? teachers[(threadIndex + replyIndex) % teachers.Count]
                    : students[(threadIndex * 3 + replyIndex * 5) % students.Count];
            }

            return (replyIndex % 6) switch
            {
                0 => teachers[(threadIndex + replyIndex) % teachers.Count],
                1 => admins[(threadIndex + replyIndex) % admins.Count],
                _ => students[(threadIndex * 4 + replyIndex * 7) % students.Count]
            };
        }

        private static string BuildReplyContent(ThreadSeed seed, Role role, int replyIndex)
        {
            var fragment = role switch
            {
                Role.Admin => AdminReplyFragments[(replyIndex + seed.DaysAgo) % AdminReplyFragments.Length],
                Role.Teacher => TeacherReplyFragments[(replyIndex + seed.DaysAgo) % TeacherReplyFragments.Length],
                _ => StudentReplyFragments[(replyIndex + seed.DaysAgo) % StudentReplyFragments.Length]
            };

            if (seed.Title.StartsWith("[News]", StringComparison.OrdinalIgnoreCase))
            {
                return $"{fragment} Благодаря за информацията по темата \"{seed.RootTitle}\".";
            }

            return $"{fragment} Темата \"{seed.RootTitle}\" е важна за ежедневието в МГ \"Академик Кирил Попов\".";
        }

        private sealed record TeacherSeed(string Username, string Email);

        private sealed record ThreadSeed(
            string Title,
            string RootTitle,
            string RootContent,
            Role CreatorRole,
            int DaysAgo,
            bool IsPinned,
            bool IsLocked,
            int ReplyCount);

        private sealed record PinLocationSeed(
            string LayerId,
            double X,
            double Y,
            string ZoneLabel,
            string Category,
            int Count,
            string[] Titles,
            string[] Descriptions);
    }
}
