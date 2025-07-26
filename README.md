# E-Commerce API (.NET 9.0)

A comprehensive, production-ready E-Commerce REST API built using .NET 9.0, designed for modern online stores and retail platforms. This project features robust authentication, product and order management, advanced background jobs, and scalable caching, following industry best practices and clean architecture.

## Features

- **.NET 9.0 (C# preview features)**
- **Entity Framework Core** with MySQL (Pomelo provider)
- **Repository pattern** with Unit of Work
- **ASP.NET Core Identity** for authentication
- **JWT Bearer authentication** (secure, stateless)
- **Admin/User role separation**
- **Hangfire** for background jobs
- **Redis** for distributed caching
- **AutoMapper** for DTO/entity mapping
- **Serilog** for structured logging
- **Swagger/Scalar** for API documentation
- **Comprehensive Controllers** (Account, Admin, Cart, Category, Collection, CustomerAddress, Discount, Order, Product, ProductVariant, SubCategory, WareHouse)
- **Extensive DTO and Entity models**
- **Middleware** for global error handling
- **Security best practices**

## Installation

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [MySQL Server](https://dev.mysql.com/downloads/)
- [Redis](https://redis.io/download)

### Setup Steps
1. **Clone the repository:**
   ```bash
   git clone https://github.com/omargamal1121/Fashion.git
   cd E-Commerce-API-main
   ```
2. **Configure the database:**
   - Update the MySQL connection string in `appsettings.json`.
   - Ensure Redis connection is configured in `appsettings.json`.
3. **Apply migrations:**
   ```bash
   dotnet ef database update
   ```
4. **Run the API:**
   ```bash
   dotnet run
   ```
5. **Access Swagger UI:**
   - Navigate to `http://localhost:5000/swagger` (or the port specified in your launch settings).

## Usage

- **Authentication:**
  - Register/login via `/api/account/register` and `/api/account/login`.
  - Use JWT tokens for secure API access.
- **Product Management:**
  - Endpoints for CRUD operations on products, categories, variants, and collections.
- **Cart & Order:**
  - Add/remove items from cart, place orders, manage addresses.
- **Admin Operations:**
  - Manage users, products, discounts, and warehouse inventory.
- **Background Jobs:**
  - Hangfire dashboard available at `/hangfire` (if enabled).
- **API Documentation:**
  - Interactive docs via Swagger/Scalar.

## Project Structure

- `Controllers/` — API endpoints
- `Services/` — Business logic
- `Models/` — Entity models
- `DTOs/` — Data transfer objects
- `Interfaces/` — Service/repository contracts
- `Repositories/` — Data access layer
- `Middleware/` — Error handling, logging
- `Docs/` — System and security documentation

## Tech Stack

- **Backend:** ASP.NET Core 9.0, Entity Framework Core (MySQL)
- **Authentication:** ASP.NET Identity, JWT
- **Caching:** Redis
- **Background Jobs:** Hangfire
- **Logging:** Serilog
- **API Docs:** Swagger, Scalar

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

**Maintainer:** [omargamal1121](https://github.com/omargamal1121)

For questions, issues, or contributions, please open an issue or submit a pull request on GitHub.
