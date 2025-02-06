# GenerateInitials - Azure Function

## Overview

GenerateInitials is an Azure Function that generates unique employee initials based on a given name and stores them in an Azure SQL database. The function employs a retry mechanism to ensure uniqueness while avoiding inappropriate or restricted initials.

## Features

- Generates initials based on multiple strategies.
- Stores generated initials in an Azure SQL database.
- Handles SQL timeout and connection issues with retries.
- Prevents the use of restricted or inappropriate initials.
- Logs execution details for monitoring and debugging.

## Strategies for Initial Generation

The function follows a structured approach to generate initials:

1. First 2 letters of the first name + first 3 letters of the last name.
2. First 3 letters of the first name + first 2 letters of the last name.
3. First 4 letters of the first name + first 1 letter of the last name.
4. First 1 letter of the first name + first 4 letters of the last name.
5. First 5 letters of the first name.
6. First 5 letters of the last name.
7. Combination of first, middle, and last name characters.
8. Another variation including the middle name.
9. A final fallback strategy before failing.

## Prerequisites

- Azure account with an active subscription.
- Azure Functions setup.
- Azure SQL Database.
- Connection string stored in **Application Settings** (`SqlConnectionString`).
- .NET 6+ environment for local development.

## Installation and Deployment

### 1. Clone the Repository

```sh
    git clone https://github.com/yourusername/GenerateInitials.git
    cd GenerateInitials
```

### 2. Configure Azure SQL Connection String

- Ensure you have an **Azure SQL Database**.
- Store the connection string as an **Application Setting** in your Azure Function App (`SqlConnectionString`).

### 3. Deploy to Azure

```sh
    func azure functionapp publish <your-function-app-name>
```

## API Usage

### Request

- Endpoint: `POST /api/GenerateInitials`
- Headers: `Content-Type: application/json`
- Body:

```json
{
    "EmployeeName": "John Doe"
}
```

### Response

#### Success (200 OK)

```json
{
    "EmployeeInitials": "JODOE"
}
```

#### Conflict (409 Conflict) - No Unique Initials Found

```json
{
    "error": "Unable to generate unique initials after multiple attempts."
}
```

#### Bad Request (400 Bad Request) - Missing Name

```json
{
    "error": "EmployeeName is required."
}
```



## Author

Developed by **Mikkel Bank Bjerregaard**

---

This project was created as part of an exploration into Azure Functions and serverless computing. 🚀

