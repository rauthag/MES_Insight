# 📊 MES Insight

A desktop tool for analyzing and visualizing response times of MES (Manufacturing Execution System) between MES PC and PLC. It helps identify performance bottlenecks, communication lags, and system stability issues.

## 📸 Screenshots

### Main Dashboard & Analytics
<img src="https://github.com/user-attachments/assets/aa3f7b9e-8119-4941-b0ca-321b89a04047" width="850" />
 <img src="https://github.com/user-attachments/assets/0a8bd069-ec32-42f3-84c9-f5a5955fb6af" width="850" />

*Overview with statistical distribution and stability scoring.*

---

### Pre-analysis Configuration
<p align="left">
  <img src="https://github.com/user-attachments/assets/7c0a684b-092f-4f81-9b7e-9086f6d5f75e" width="850" /> 
  <img src="https://github.com/user-attachments/assets/28c76b17-a785-4d8d-89ff-f0698b832954" width="850" />
</p>

*Filter stations and message types before loading data to ensure precision and speed.*

---

### Data Management & Long Tail Detail
<p align="left">
  <img src="https://github.com/user-attachments/assets/410322dc-53bf-469e-8e4e-3386b82dd978" width="850" />
</p>

*Left: Remote connection management. Right: Detailed outlier visualization.*

---

## 🚀 Updates & Changelog

### [April 24, 2026] - Performance & Connectivity Update
* **Remote Connectivity:** Fixed "Add Connect" functionality and improved remote station loading logic.
* **Data Cleaning:** Fixed an issue where irrelevant system files (metadata/logs) were being loaded.
* **Pre-loading Configuration:** Added a new selection screen for choosing specific **Stations** and **Station Types** before the heavy data loading process begins.
* **UI/UX Fixes:** Improved "Long Tail" chart granularity for better outlier visualization (finer buckets).
* **General Fixes:** Optimization of parsing logic and UI responsiveness.

## ✨ Key Features
* **Log Parsing:** Automatically processes large volumes of log files.
* **Statistical Analysis:** Calculates Average, P95 (95th percentile), Min/Max, and Standard Deviation.
* **Visual Distribution:** Generates dynamic charts showing the main distribution and "Long Tail" outliers.
* **Stability Scoring:** Evaluates system reliability using Coefficient of Variation (CV).
* **Smart Filtering:** Filter by message types (UNIT_INFO, UNIT_RESULT, etc.) and specific stations.

## 🛠️ How it Works
1.  **Select Source:** Connect to a remote server or local directory.
2.  **Filter:** Choose the station and message types you want to analyze in the pre-load screen.
3.  **Analyze:** The app parses logs and displays interactive charts.
4.  **Evaluate:** Use the "Stability" indicator to check if the station meets performance KPIs.

## 💻 Technologies
* **C# / .NET / WPF**
* **LiveCharts** (for data visualization)

---
*Developed for industrial automation monitoring and MES performance optimization.*
