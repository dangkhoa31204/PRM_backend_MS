# 🚀 Hướng Dẫn Triển Khai PRM Backend Lên VPS (Production Deployment Guide)

Tài liệu này hướng dẫn chi tiết từng bước để deploy toàn bộ hệ thống microservices lên VPS chạy Ubuntu Server dưới **một đường link (domain) duy nhất**.

---

## 📋 Yêu cầu chuẩn bị trên VPS
Khuyên dùng hệ điều hành **Ubuntu 20.04** hoặc **22.04 LTS**.

### 1. Cài đặt các công cụ cần thiết trên VPS
Chạy các lệnh sau bằng quyền root hoặc `sudo` trên VPS:

```bash
# Cập nhật package list
sudo apt update && sudo apt upgrade -y

# Cài đặt Git, curl và các công cụ cơ bản
sudo apt install -y git curl unzip

# Cài đặt Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Cài đặt Nginx
sudo apt install -y nginx

# Cài đặt Certbot (dành cho SSL/HTTPS)
sudo apt install -y certbot python3-certbot-nginx
```

---

## 🛠️ Quy trình Triển khai chi tiết

### Bước 1: Clone dự án và Cấu hình Môi trường `.env`
1. Di chuyển vào thư mục mong muốn trên VPS (ví dụ `/var/www/` hoặc `/home/ubuntu/`):
   ```bash
   cd /var/www
   git clone <URL_REPO_CỦA_BẠN> prm-backend
   cd prm-backend
   ```
2. Tạo file cấu hình môi trường `.env` thực tế từ file mẫu:
   ```bash
   cp .env.example .env
   ```
3. Chỉnh sửa file `.env` bằng trình soạn thảo `nano` hoặc `vim`:
   ```bash
   nano .env
   ```
   * *Điền các thông tin mật khẩu DB bảo mật.*
   * *Đổi `JWT_KEY` thành một chuỗi bảo mật dài.*
   * *Thay đổi `APP_BASE_URL` thành domain hoặc IP Public của VPS (ví dụ: `http://103.x.x.x:5000` hoặc `https://api.prm-order.com`).*
   * *Nhấn `Ctrl + O` -> `Enter` để lưu, `Ctrl + X` để thoát `nano`.*

---

### Bước 2: Chạy hệ thống bằng Docker Compose
1. Khởi chạy toàn bộ dịch vụ (gồm 3 Postgres DB, 3 Service Backend, 1 API Gateway) ở chế độ chạy ngầm (detached mode):
   ```bash
   docker compose -f docker-compose.prod.yml up -d --build
   ```
2. Kiểm tra danh sách các container đang chạy và trạng thái của chúng:
   ```bash
   docker ps
   ```
   *Bạn sẽ thấy các container chạy ở port `127.0.0.1` nội bộ và duy nhất Gateway chạy công khai ở cổng `:5000`.*
3. Kiểm tra log của các service để đảm bảo không có lỗi kết nối database và migrations đã chạy thành công:
   ```bash
   docker logs prm-identity-prod
   docker logs prm-restaurant-prod
   docker logs prm-order-prod
   ```

---

### Bước 3: Cấu hình Nginx Reverse Proxy (Gộp 3 Service thành 1 Link)
Để người dùng (Flutter Client) chỉ gọi qua port `80` hoặc `443` (HTTPS) và tự động trỏ đến các API tương ứng:

1. Sao chép cấu hình Nginx mẫu từ dự án vào thư mục cấu hình của Nginx trên VPS:
   ```bash
   sudo cp nginx.conf /etc/nginx/sites-available/prm-backend
   ```
2. Mở file cấu hình để cập nhật thông tin Domain hoặc IP:
   ```bash
   sudo nano /etc/nginx/sites-available/prm-backend
   ```
   * *Thay thế dòng `server_name yourdomain.com api.yourdomain.com;` bằng IP VPS hoặc Tên miền của bạn.*
3. Kích hoạt cấu hình bằng cách tạo symbolic link:
   ```bash
   sudo ln -s /etc/nginx/sites-available/prm-backend /etc/nginx/sites-enabled/
   ```
4. Xóa cấu hình mặc định (default) của Nginx để tránh xung đột cổng 80:
   ```bash
   sudo rm /etc/nginx/sites-enabled/default
   ```
5. Kiểm tra tính hợp lệ của file cấu hình Nginx:
   ```bash
   sudo nginx -t
   ```
   *Nếu hiển thị `syntax is ok` và `test is successful` thì cấu hình chính xác.*
6. Tải lại cấu hình Nginx:
   ```bash
   sudo systemctl reload nginx
   ```

---

### Bước 4: Cài đặt SSL (HTTPS) miễn phí (Khuyên dùng nếu có tên miền)
Chạy lệnh sau để cấu hình SSL tự động bằng Certbot:
```bash
sudo certbot --nginx -d yourdomain.com -d api.yourdomain.com
```
*Làm theo hướng dẫn trên màn hình. Certbot sẽ tự động đăng ký SSL từ Let's Encrypt và ghi đè cấu hình HTTPS vào Nginx giúp bạn.*

---

### Bước 5: Kiểm tra và Sử dụng
Bây giờ, mọi API đã chạy dưới **1 đường dẫn duy nhất**:

1. **Test Service Identity**:
   ```bash
   curl -i http://localhost/api/auth/login
   # hoặc từ bên ngoài:
   curl -i https://api.yourdomain.com/api/auth/login
   ```
   *Nginx sẽ chuyển tiếp đến API Gateway (Port 5000), Gateway sẽ định tuyến sang Identity Service (Port 5001) và xử lý.*

2. **Test kết nối Database & Tài khoản Admin mặc định**:
   Admin account được tạo tự động trên `Identity Service` khi chạy lần đầu:
   * **Username**: `admin`
   * **Password**: `admin123`
   
   Bạn có thể đăng nhập bằng API:
   ```bash
   curl -X POST https://api.yourdomain.com/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"usernameOrEmail": "admin", "password": "admin123"}'
   ```
   *Nếu trả về JWT Token thành công, hệ thống của bạn đã chạy hoàn hảo!*

3. **Flutter Client**:
   * Cập nhật URL API của Flutter trỏ tới domain/IP duy nhất của bạn: `https://api.yourdomain.com/` (hoặc `http://<IP-VPS>/`).
   * Các route SignalR trỏ đến: `https://api.yourdomain.com/hubs/staff`
