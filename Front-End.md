# Gambaran Sistem ASMARA (Asset Manajemen Sarana)

**ASMARA** merupakan sistem *ticketing* internal perusahaan yang menggabungkan dua proses utama:

1. **Procurement** — Pengajuan pembelian barang oleh *Employee*, persetujuan *Department Manager* dan *Finance*, hingga *Purchasing* melakukan pemesanan dan *Inventory* menerima barang.
2. **Maintenance** — Pengajuan perbaikan aset, pemeriksaan oleh *Department Manager*, penugasan teknisi oleh *IT Support*, proses perbaikan, hingga pengembalian aset kepada *requester*.

Kedua proses tersebut saling terhubung. Ketika teknisi membutuhkan *spare part* yang tidak tersedia di *Inventory*, prosesnya dapat dilanjutkan melalui *Procurement*.

BPMN menggunakan tiga *participant*: **Procurement Flow**, **Maintenance Flow**, dan **General**. *Activity Diagram* kemudian menguraikan prosesnya menjadi beberapa aktivitas berdasarkan peran masing-masing.

---

## 1. Aktor dan Hak Akses

Dari *Activity Diagram* dan *Use Case Diagram*, diidentifikasi peran-peran berikut beserta tanggung jawab utamanya:

| Role | Tanggung Jawab Utama |
| :--- | :--- |
| **Employee** | Membuat dan memantau tiket, melakukan revisi, menerima barang. |
| **Department Manager** | Review dan *approve*, *reject*, atau *revise* tiket. |
| **Finance** | Review dan memberikan keputusan pada *Procurement*. |
| **Purchasing** | Memproses *order* dan menerima barang. |
| **Inventory** | Mengelola stok, menerima barang, dan menangani kebutuhan *spare part*. |
| **IT Support** | Review tiket *Maintenance* dan menetapkan teknisi. |
| **Relevant Employee** | Menangani perbaikan aset dan melaporkan hasilnya. |
| **Division Manager** | Mengetahui dan melakukan *acknowledgment* proses *dispose/archive*. |

Hak akses setiap role tidak boleh dibuat sama. Sebagai contoh:
- **Employee** dapat membuat tiket dan melihat tiket miliknya.
- **Department Manager** memiliki akses untuk membaca dan memberikan keputusan terhadap tiket bawahannya.
- **Finance** berfokus pada persetujuan *Procurement*.
- **IT Support** memiliki akses untuk melakukan *assignment* teknisi.
- **Relevant Employee** berfokus pada tiket yang ditugaskan kepadanya.
- **Inventory** berfokus pada stok dan kebutuhan barang.
- **Division Manager** memiliki akses untuk melihat informasi dan mengetahui proses *dispose/archive*.

> **Catatan:** Pada *Use Case Diagram*, hak akses ditunjukkan menggunakan konsep **CRU (Create, Read, Update)**. Ada pula beberapa akses *Read Only* dan *Read*. Jadi, implementasi permission sebaiknya mengikuti matriks hak akses, bukan hanya menyembunyikan menu.

---

## 2. Modul Front-end yang Perlu Dibuat

Berdasarkan diagram sistem, pembagian modul front-end direncanakan sebagai berikut:

### **General**
- Login / Logout
- Dashboard
- Profile
- Notification

### **Procurement**
- Procurement Ticket
- Create Ticket
- Ticket Detail
- Approval Department Manager
- Approval Finance
- Revise Ticket
- Purchasing / Order
- Goods Receiving
- Inventory Registration

### **Maintenance**
- Maintenance Ticket
- Create Ticket
- Department Review
- IT Support Assignment
- Assigned Maintenance
- Repair Report
- Inventory Part Request
- Item Delivery
- Completion Review
- Dispose / Archive

### **Master Data & Management**
- Employee
- Department
- Account
- Role
- Workflow
- Workflow Step
- Item Category
- Item Inventory
- Asset

> Tidak semua menu tersebut harus ditampilkan kepada semua role. Sidebar dan halaman yang tersedia harus menyesuaikan role pengguna yang login.

---

## 3. Alur Proses untuk Halaman Front-end

### **A. Alur Procurement**
1. **Employee** membuat *Procurement Ticket*.
2. **Employee** mengisi *Subject*, memilih item dari katalog atau memasukkan item manual, lalu mengisi *Quantity* dan *Price*.
3. Ticket dikirim ke **Department Manager**.
4. **Department Manager** dapat:
   - **Approve** $\rightarrow$ diteruskan ke Finance.
   - **Reject** $\rightarrow$ tiket ditolak.
   - **Revise** $\rightarrow$ dikembalikan kepada Employee.
5. **Finance** melakukan review dan memberikan keputusan yang sama: *Approve*, *Reject*, atau *Revise*.
6. Setelah disetujui Finance, **Purchasing** memproses *order*.
7. Barang diterima dan diregistrasikan ke **Inventory**.
8. Ticket ditutup dan **Employee** menerima notifikasi.

*Status penting pada Activity Diagram Procurement:* `PendingApproval`, `RevisionRequired`, `Approved`, `Rejected`, `Ordered`, `GoodsReceived`, dan `Sent to Inventory`.

---

### **B. Alur Maintenance**
Alur ini lebih panjang karena melibatkan aset dan teknisi:
1. **Employee** membuat *Maintenance Ticket* dengan memilih aset dan mengisi deskripsi masalah.
2. **Department Manager** melakukan review dan dapat *approve*, *reject*, atau *revise*.
3. Jika disetujui, tiket diteruskan kepada **IT Support**.
4. **IT Support** melakukan review dan menentukan **Relevant Employee** atau teknisi.
5. Requester mengirimkan aset kepada teknisi.
6. Teknisi menerima aset dan melakukan pemeriksaan serta perbaikan.
7. Jika membutuhkan *spare part*, teknisi meminta **Inventory** melakukan pengecekan stok.
8. Jika barang tersedia, **Inventory** mengirimkan *spare part* kepada teknisi.
9. Jika barang tidak tersedia, proses diarahkan ke **Procurement**.
10. Teknisi mengirimkan laporan hasil perbaikan.
11. Jika aset dapat diperbaiki, aset dikembalikan kepada requester.
12. Jika tidak dapat diperbaiki, proses dapat dilanjutkan ke *Dispose* atau *Archive* dengan keterlibatan **Division Manager**.
13. Requester menerima aset dan melakukan konfirmasi penerimaan.

---

## 4. Struktur Data dan Entity Relationship (ERD)

ERD memiliki beberapa kelompok entitas penting:

| Kelompok Entitas | Entitas |
| :--- | :--- |
| **Account & Authorization** | Account, Role, AccountRole |
| **Organization** | Department, Employee |
| **Workflow** | Workflow, WorkflowStep |
| **Ticket Transaction** | Ticket, TicketWorkflowLog |
| **Procurement** | ProcurementDetail |
| **Maintenance** | MaintenanceDetail |
| **Inventory** | ItemInventory, ItemCategory |
| **Asset** | Asset |

### **Relasi Kunci Front-end**
- **Employee** berhubungan dengan **Department**.
- **Employee** memiliki relasi `ManagerId` dengan Employee lainnya.
- **Ticket** berhubungan dengan **Employee** sebagai pembuat tiket.
- **Ticket** dapat memiliki `ParentTicketId` untuk menghubungkan tiket (misal: Maintenance ke Procurement).
- **Ticket** memiliki `TicketWorkflowLog` untuk menyimpan riwayat proses.
- **ProcurementDetail** menyimpan rincian barang dan harga.
- **MaintenanceDetail** berhubungan dengan **Asset**.
- **Asset** memiliki status seperti `UnderMaintenance`, `InUsedBy`, `Disposed`, dan `Archived`.
- **ItemInventory** berhubungan dengan **ItemCategory**.

> Halaman **Ticket Detail** sebaiknya tidak hanya menampilkan *Subject* dan *Description*, tetapi juga menyediakan informasi pembuat, status, riwayat workflow, detail barang atau aset, dan relasi tiket jika ada.

---

## 5. Rencana Pengerjaan Front-end

Pengembangan front-end dilakukan secara bertahap:

1. **Project Setup** — Menentukan framework, struktur folder, routing, UI library, dan konfigurasi API.
2. **Authentication** — Login, logout, serta penyimpanan informasi user dan role.
3. **Dashboard & Layout** — Sidebar dinamis, navbar, dashboard, dan notification.
4. **Procurement Module** — Mulai dari *create ticket* sampai *purchasing* dan *inventory*.
5. **Maintenance Module** — Mulai dari *create ticket* sampai *repair*, *disposal*, dan *archive*.
6. **Master Data Module** — Employee, department, account, role, asset, dan inventory.
7. **Integration & Testing** — Menghubungkan halaman dengan backend dan menguji hak akses (authorization).

---

### 📋 DAFTAR ROLE & PEMETAAN HALAMAN FRONTEND ASMARA

---

## 👥 1. Daftar Role Pengguna
Sistem ASMARA menggunakan role dasar berikut (*satu akun dapat memiliki lebih dari satu role sesuai kebutuhan*):

| No. | Role | Tanggung Jawab Utama |
| :---: | :--- | :--- |
| **1** | **Employee / Requester** | Membuat dan memantau *ticket*. |
| **2** | **Department Manager** | *Review* dan *approval ticket* dari departemennya. |
| **3** | **Finance** | Melakukan *approval Procurement*. |
| **4** | **Purchasing** | Memproses pembelian barang. |
| **5** | **Inventory** | Mengelola stok dan penerimaan barang. |
| **6** | **IT Support** | Mengelola *ticket Maintenance* dan *assignment* teknisi. |
| **7** | **Relevant Employee / Technician** | Melakukan perbaikan aset. |
| **8** | **Division Manager** | Mengetahui dan menyetujui proses *disposal/archive*. |
| **9** | **System Admin** *(Konfirmasi)* | Mengelola master data dan konfigurasi sistem. |

---

## 🖥️ 2. Pemetaan Modul & Halaman Frontend

Frontend ASMARA dibagi menjadi 6 kelompok menu utama:

### **A. General (Lintas Role)**
* **Login** — Autentikasi pengguna *(Semua pengguna)*
* **Dashboard** — Ringkasan *ticket* dan aktivitas (menyesuaikan role) *(Semua role)*
* **My Profile** — Informasi akun pengguna *(Semua role)*
* **Notifications** — Notifikasi *ticket* dan *approval* *(Semua role)*
* **My Activities** — Riwayat aktivitas pengguna *(Semua role)*

### **B. Procurement**
* **Procurement Dashboard** — Ringkasan *ticket Procurement* *(Role terkait)*
* **My Procurement Tickets** — *Ticket* yang dibuat pengguna *(Employee)*
* **Create Procurement Ticket** — Membuat permintaan barang *(Employee)*
* **Procurement Ticket Detail** — Melihat detail dan riwayat *ticket* *(Role terkait)*
* **Procurement Approval** — *Review, approve, reject, revise* *(Department Manager)*
* **Finance Approval** — *Approval* dari Finance *(Finance)*
* **Purchasing Management** — Memproses pesanan barang *(Purchasing)*
* **Goods Receiving** — Konfirmasi barang diterima *(Purchasing)*
* **Inventory Registration** — Registrasi barang ke *inventory* *(Inventory)*
* **Procurement History** — Riwayat *ticket Procurement* *(Role terkait)*

### **C. Maintenance**
* **Maintenance Dashboard** — Ringkasan *ticket Maintenance* *(Role terkait)*
* **My Maintenance Tickets** — *Ticket* yang dibuat pengguna *(Employee)*
* **Create Maintenance Ticket** — Membuat laporan kerusakan aset *(Employee)*
* **Maintenance Ticket Detail** — Detail dan *timeline ticket* *(Role terkait)*
* **Maintenance Approval** — *Review, approve, reject, revise* *(Department Manager)*
* **Technician Assignment** — Menentukan teknisi *(IT Support)*
* **Assigned Maintenance** — Daftar pekerjaan teknisi *(Relevant Employee)*
* **Repair Detail** — Informasi pekerjaan perbaikan *(Relevant Employee)*
* **Repair Report** — Mengirim hasil perbaikan *(Relevant Employee)*
* **Spare Part Request** — Meminta *spare part* *(Relevant Employee)*
* **Inventory Part Processing** — Memeriksa dan mengirim *spare part* *(Inventory)*
* **Item Handover** — Konfirmasi serah terima barang *(Employee / Requester)*
* **Disposal & Archive Review** — *Review disposal/archive* *(Division Manager)*
* **Maintenance History** — Riwayat perbaikan aset *(Role terkait)*

### **D. Inventory & Asset Management**
* **Item Inventory / Detail / Receive / Stock Movement** — Pengelolaan stok barang gudang *(Inventory)*
* **Item Categories** — Mengelola kategori barang *(Inventory / Admin)*
* **Asset List / Detail / History** — Daftar, detail, dan riwayat aset *(Role terkait)*
* **Asset Management** — Membuat dan memperbarui data aset *(Admin / Role tertentu)*
> *Catatan: Item Inventory (stok/spare part) dibedakan dengan Asset (barang yang digunakan organisasi).*

### **E. Master Data & Administration**
* **Employee / Department / Account / Role / Account Role Management** *(Admin)*
* **Workflow / Workflow Step Management** *(Admin)*

### **F. Reports & History**
* **Procurement Report** *(Finance / Admin)*
* **Maintenance Report** *(IT Support / Admin)*
* **Inventory Report** *(Inventory / Admin)*
* **Asset Report & Ticket History & Workflow History** *(Admin / Role terkait)*