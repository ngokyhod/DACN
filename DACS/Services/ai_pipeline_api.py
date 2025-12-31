from flask import Flask, request, jsonify
from fuzzywuzzy import fuzz
import pyodbc
from flask_cors import CORS
app = Flask(__name__)
CORS(app)
# 🔌 Cấu hình kết nối SQL Server
conn_str = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=HOA230969\\SQLEXPRESS;"
    "DATABASE=QuanLyPhuPham;"
    "Trusted_Connection=yes;"
)


@app.route('/predict', methods=['POST'])
def predict():
    message = request.form.get('message', '').lower()
    tra_loi = "Xin lỗi, tôi chưa hiểu câu hỏi này."

    if not message:
        return jsonify({"response": tra_loi})

    try:
        conn = pyodbc.connect(conn_str)
        cursor = conn.cursor()
        cursor.execute("SELECT Intent, TrainingSentence, Response, ProductName FROM CauHoiThuongGap")
        rows = cursor.fetchall()

        best_match = None
        best_score = 0

        for row in rows:
            intent, sentence, response, product = row
            score = fuzz.partial_ratio(message, sentence.lower())
            if score > best_score:
                best_score = score
                best_match = {"intent": intent, "response": response, "product": product}

        if best_match and best_score > 70:
            tra_loi = best_match['response']

            if best_match['intent'] == "ban_san_pham":
                tra_loi += " <br/>👉 <a href='/Home/ThuGom' class='btn btn-success btn-sm mt-2'>Nhấn vào đây để đăng bán</a>"


            if best_match['product']:
                tra_loi += f" (Sản phẩm liên quan: {best_match['product']})"

        cursor.close()
        conn.close()

    except Exception as e:
        tra_loi = f"Lỗi kết nối CSDL: {str(e)}"

    return jsonify({"response": tra_loi, "score": best_score})

if __name__ == '__main__':
    app.run(host="0.0.0.0", port=5000)
