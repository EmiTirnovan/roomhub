# DECISIONS

Answer **yourself**, in your own words, in 3–5 sentences per question.
Refer to concrete files and classes from your own repo (for example `Features/Bookings/CreateBooking/...`).
An unanswered question gives 0 points.

## 1. A rule you moved

Pick one business rule (R1–R8). In which file did it live in the starter and in which file does it live now? Why there?

Am mutat r3 si r6 din vechiul BookingService.cs direct in metoda statica Booking.Create din Domain. In felul asta nu pot crea o rezervare invalida doar pentru ca apelez alta parte din aplicatie

## 2. Result instead of an exception

Why is `Result` a better choice than an exception for "the room is occupied"? What happens in your code when an `Error` is returned?

Folosesc Result deoarece faptul ca o sala este deja ocupata este o situatie normala in aplicatie, nu o eroare neasteptata. Endpointul verifica rezultatul si trandorma eroarea in raspousn http 409 Conflict

## 3. Who knows about EF Core?

Which classes in your project know about EF Core and which do not? What do you gain and what do you pay with Repository + Unit of Work?

EF Core este folosit in Infrastructure, unde se afla DBcontextul si implementarile concrete ale repository-ului, Domain nu depinde de

## 4. The flow of a request

Draw in text (ASCII arrows) the path of a `POST /api/rooms/{roomId}/bookings` request through your project, with the **real** names of files and classes, from the endpoint to the database.

sin api - minimal api endpoint - CreateBookingCommand - CreateBookingHandler - create() - repo - dbcontext - sqlite

## 5. What you rejected from what the AI gave you

Give a concrete example of code or an idea proposed by the AI that you corrected or rejected, and say why.

Nu am respins nimic, dar am verificat sa fie totul ok cu regulile, dar posibil sa fii scapat anumite detalii

## 6. If you had 30 more minutes

What would you improve and what risk did you leave in the project?

Daca as mai avea 30 min as completa mai bine toate md files si as reverifica si testa cate un caz manual pentru fiecare regula in swagger
