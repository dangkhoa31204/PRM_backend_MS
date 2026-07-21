# 🍽️ Aroma Bistro - QR Code Ordering & Realtime Restaurant Management System

> **Hệ thống Đặt món qua mã QR & Quản lý Nhà hàng Real-time theo kiến trúc Microservices .NET 8, Flutter và AI Qwen 2.5.**

---

## 📌 1. Tổng quan Dự án & Cấu trúc Thư mục

Dự án được xây dựng theo mô hình **Microservices Architecture** kết hợp với **Flutter Cross-Platform Client**:

```text
prmproject/
├── PRM_backend_MS/                 # Backend Microservices (.NET 8 Web API)
│   ├── src/
│   │   ├── PRM.Gateway/            # API Gateway (Ocelot Proxy, Port 5000)
│   │   ├── PRM.Services.Identity/  # JWT Auth & Account Management (Port 5001)
│   │   ├── PRM.Services.Restaurant/# Menu, Cloudinary CDN & Tables (Port 5002)
│   │   ├── PRM.Services.Order/     # Orders, Payments, SignalR Hubs (Port 5003)
│   │   └── PRM.Services.AI/        # Qwen 2.5 LLM AI Integration (Port 5004)
│   ├── tests/
│   │   └── PRM.Tests/              # Unit Test project (xUnit)
│   ├── database/
│   │   └── init_database.sql       # PostgreSQL Full Schema & Seed Data Script
│   ├── .github/workflows/
│   │   └── ci.yml                  # GitHub Actions CI/CD Pipeline
│   ├── docker-compose.yml          # Local Docker Compose setup
│   └── README.md                   # File hướng dẫn này
│
└── qr_order/                       # Frontend & Mobile Application (Flutter)
    ├── lib/                        # Dart source code (Web & Mobile views)
    └── pubspec.yaml                # Flutter dependencies
```

---

## 🛠️ 2. Công nghệ Sử dụng

* **Backend**: .NET 8 Web API, Ocelot API Gateway, Entity Framework Core, SignalR WebSockets.
* **Frontend**: Flutter 3.x (Web Client cho Khách hàng & Mobile Client cho Nhân viên/Admin).
* **Database**: PostgreSQL (Database-per-Service Pattern).
* **AI Engine**: Qwen 2.5:7b LLM via Ollama (AI Sommelier & AI Dashboard Recommendations).
* **DevOps & Cloud**: Docker, Docker Compose, GitHub Actions (CI/CD), Cloudinary CDN, SePay Webhook.

---

## 🚀 3. Hướng dẫn Cài đặt & Chạy Hệ thống bằng Docker Compose

### Yêu cầu tiên quyết:
* Máy tính đã cài đặt **Docker Desktop** và **Git**.

### Các bước khởi chạy (Chỉ 1 câu lệnh):
1. **Clone project & chuyển vào thư mục backend**:
   ```bash
   git clone <URL_REPO> prmproject
   cd prmproject/PRM_backend_MS
   ```

2. **Khởi chạy toàn bộ 8 Container (Database + Microservices + Gateway)**:
   ```bash
   docker compose up -d --build
   ```

3. **Kiểm tra trạng thái hệ thống**:
   * API Gateway: `http://localhost:5000`
   * Swagger Documentation: `http://localhost:5000/swagger`
   * PostgreSQL Databases: Cổng `5433` (Identity), `5434` (Restaurant), `5435` (Order).

---

## 🧪 4. Hướng dẫn Chạy Unit Test

Tại thư mục `PRM_backend_MS`, chạy câu lệnh:
```bash
dotnet test
```
*Hệ thống sẽ thực thi 16 Unit Test cases kiểm thử cho các module Order, Restaurant và Identity với tỉ lệ thành công 100%.*

---

## 👥 5. Phân quyền Người dùng (3 Roles)

1. **Customer (Khách hàng)**: Quét mã QR tại bàn `http://localhost:5000/?tableId=1` ➔ Xem menu ➔ Đặt món ➔ Gọi thêm món ➔ Trò chuyện với AI Sommelier ➔ Thanh toán VietQR ➔ Đánh giá 5 sao.
2. **Staff (Nhân viên)**: Đăng nhập `staff` / `staff123` ➔ Nhận thông báo đơn hàng real-time qua SignalR ➔ Chuyển trạng thái chế biến (Xác nhận ➔ Nấu món ➔ Phục vụ).
3. **Admin (Quản lý)**: Đăng nhập `admin` / `admin123` ➔ Quản lý menu (thêm/sửa món + upload ảnh Cloudinary) ➔ Tạo bàn & xuất mã QR ➔ Quản lý tài khoản nhân viên ➔ Xem biểu đồ doanh thu & AI Recommendations.

---

## 📑 6. Danh mục Sản phẩm Bàn giao (Deliverables)

| STT | Sản phẩm | Vị trí trong Project |
| :--- | :--- | :--- |
| 1 | **Source Code Backend** | `PRM_backend_MS/src/` |
| 2 | **Source Code Mobile/Web** | `qr_order/lib/` |
| 3 | **File Database Script** | `PRM_backend_MS/database/init_database.sql` |
| 4 | **Docker & CI/CD** | `docker-compose.yml` & `.github/workflows/ci.yml` |
| 5 | **Báo cáo PDF (Report 7)** | `capstone_project_document.md` |
| 6 | **Unit Test Project** | `PRM_backend_MS/tests/PRM.Tests/` |
