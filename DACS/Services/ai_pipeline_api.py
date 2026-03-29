# -*- coding: utf-8 -*-
from flask import Flask, request, jsonify
from fuzzywuzzy import fuzz
import pyodbc
from flask_cors import CORS
import datetime
import math

app = Flask(__name__)
CORS(app)


def normalize_text(value):
    return (value or '').strip().lower()


def split_needs(raw_needs):
    return [item.strip().lower() for item in (raw_needs or '').split(',') if item.strip()]


def get_best_need_score(product_name, needs):
    best_score = 0
    for need in needs:
        partial_score = fuzz.partial_ratio(product_name, need)
        ratio_score = fuzz.ratio(product_name, need)
        score = (partial_score + ratio_score) / 2
        if score > best_score:
            best_score = score
    return best_score


def score_location_match(target_text, references):
    normalized_target = normalize_text(target_text)
    if not normalized_target:
        return 0

    score = 0
    for reference, weight in references:
        normalized_reference = normalize_text(reference)
        if not normalized_reference:
            continue
        if normalized_target == normalized_reference:
            score = max(score, weight)
        else:
            fuzzy_score = fuzz.partial_ratio(normalized_target, normalized_reference)
            if fuzzy_score >= 90:
                score = max(score, weight * 0.8)
            elif fuzzy_score >= 75:
                score = max(score, weight * 0.5)
    return score


def parse_iso_datetime(value):
    if not value:
        return None
    try:
        return datetime.datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        return None


def parse_float(value):
    if value is None or value == '':
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def haversine_km(lat1, lng1, lat2, lng2):
    radius_km = 6371.0
    lat1_rad = math.radians(lat1)
    lng1_rad = math.radians(lng1)
    lat2_rad = math.radians(lat2)
    lng2_rad = math.radians(lng2)

    delta_lat = lat2_rad - lat1_rad
    delta_lng = lng2_rad - lng1_rad

    a = math.sin(delta_lat / 2) ** 2 + math.cos(lat1_rad) * math.cos(lat2_rad) * math.sin(delta_lng / 2) ** 2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    return radius_km * c


def score_distance_match(request_lat, request_lng, warehouse_lat, warehouse_lng):
    if None in (request_lat, request_lng, warehouse_lat, warehouse_lng):
        return None, 0

    distance_km = haversine_km(request_lat, request_lng, warehouse_lat, warehouse_lng)
    if distance_km <= 3:
        score = 70
    elif distance_km <= 8:
        score = 55
    elif distance_km <= 15:
        score = 40
    elif distance_km <= 25:
        score = 25
    else:
        score = 10

    return distance_km, score


def score_enterprise_location(distance_km, same_province, province_fuzzy_score, address_fuzzy_score):
    score = 0

    if distance_km is not None:
        if distance_km <= 3:
            score += 30
        elif distance_km <= 10:
            score += 24
        elif distance_km <= 25:
            score += 16
        elif distance_km <= 50:
            score += 6
        elif distance_km <= 100:
            score -= 8
        elif distance_km <= 300:
            score -= 20
        else:
            score -= 35

    if same_province:
        score += 10
    elif province_fuzzy_score >= 85:
        score += 4
    elif province_fuzzy_score > 0:
        score -= 6

    if address_fuzzy_score >= 90:
        score += 8
    elif address_fuzzy_score >= 75:
        score += 4

    return score


def build_warehouse_reason(location_score, urgency_score, quantity_score):
    reasons = []
    if location_score >= 60:
        reasons.append('cùng khu vực thu gom')
    elif location_score >= 35:
        reasons.append('gần khu vực thu gom')

    if urgency_score >= 18:
        reasons.append('cần nhập kho sớm')
    elif urgency_score >= 10:
        reasons.append('nên ưu tiên trong ngày')

    if quantity_score >= 10:
        reasons.append('khối lượng tương đối lớn')

    return ', '.join(reasons) if reasons else 'phù hợp để tiếp nhận và kiểm định'


def build_warehouse_reason_with_distance(location_score, urgency_score, quantity_score, distance_km):
    reasons = []
    if distance_km is not None:
        reasons.append(f'cách điểm lấy khoảng {distance_km:.1f} km')
    reason_tail = build_warehouse_reason(location_score, urgency_score, quantity_score)
    if reason_tail:
        reasons.append(reason_tail)
    return ', '.join(reasons)

# 🔌 Cấu hình kết nối SQL Server
conn_str = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=HOA230969\\\\SQLEXPRESS;"
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

@app.route('/optimize-collection', methods=['POST'])
def optimize_collection():
    data = request.json
    yeu_cau_list = data.get('yeu_cau_list', [])
    doanh_nghiep_needs = data.get('doanh_nghiep_needs', [])  # list of strings  

    results = []

    for yc in yeu_cau_list:
        m_yeucau = yc.get('m_yeucau')
        so_luong = yc.get('so_luong', 0)
        thoi_gian_ton = yc.get('thoi_gian_ton_toida', 3)
        ten_san_pham = yc.get('ten_san_pham', '').lower()
        dac_tinh_do_am = yc.get('dac_tinh_do_am', False)

        # 1. Tính điểm cơ bản (Base score based on quantity and time)
        # Số lượng càng lớn điểm càng cao, max ~ 40đ (giả sử max 1000kg)        
        score_quantity = min((so_luong / 1000) * 40, 40)

        # Thời gian tồn: càng nhỏ càng khẩn cấp
        score_urgency = 0
        if thoi_gian_ton <= 1 or dac_tinh_do_am:
            score_urgency = 40
        elif thoi_gian_ton <= 3:
            score_urgency = 20
        else:
            score_urgency = 5

        # 2. Demand Matching (Doanh nghiệp đang cần)
        score_demand_match = 0
        for need in doanh_nghiep_needs:
            # So sánh tên sản phẩm nông dân nộp với nhu cầu của doanh nghiệp    
            match_ratio = fuzz.partial_ratio(ten_san_pham, need.lower())        
            if match_ratio > 80:
                score_demand_match = 20
                break

        total_score = min(score_quantity + score_urgency + score_demand_match, 100)                                                                             
        action = "Gom bình thường"
        if total_score >= 80:
            action = "Khẩn cấp - Gom ngay (Rủi ro hỏng/Cầu cao)"
        elif total_score >= 50:
            action = "Ưu tiên gom trong ngày"

        results.append({
            'm_yeucau': m_yeucau,
            'ai_priority_score': round(total_score, 1),
            'ai_suggested_action': action
        })

    return jsonify({'results': results})

@app.route('/enterprise-matching', methods=['POST'])
def enterprise_matching():
    data = request.json
    yeu_cau_list = data.get('yeu_cau_list', [])
    nhu_cau = data.get('nhu_cau', '')
    tinh_doanh_nghiep = data.get('tinh_doanh_nghiep', '')

    if not nhu_cau:
        return jsonify({'results': []})

    results = []

    needs = split_needs(nhu_cau)

    for yc in yeu_cau_list:
        ten_san_pham = normalize_text(yc.get('ten_san_pham'))
        tinh_nong_dan = yc.get('tinh_thanh', '')

        base_match_score = get_best_need_score(ten_san_pham, needs)

        if base_match_score >= 40:
            final_score = base_match_score
            
            if tinh_doanh_nghiep and tinh_nong_dan:
                if normalize_text(tinh_doanh_nghiep) == normalize_text(tinh_nong_dan):
                    final_score += 20
                else:
                    final_score -= 15
            
            final_score = max(0, min(100, final_score))
            
            if final_score >= 40:
                results.append({
                    'm_yeucau': yc.get('m_yeucau'),
                    'match_score': round(final_score, 1)
                })

    results = sorted(results, key=lambda x: x['match_score'], reverse=True)

    return jsonify({'results': results})


@app.route('/warehouse-matching', methods=['POST'])
def warehouse_matching():
    data = request.json
    yeu_cau_list = data.get('yeu_cau_list', [])
    kho_list = data.get('kho_list', [])

    if not yeu_cau_list or not kho_list:
        return jsonify({'results': []})

    results = []
    now = datetime.datetime.utcnow()

    for yc in yeu_cau_list:
        best_match = None
        so_luong = float(yc.get('so_luong', 0) or 0)
        san_sang_at = parse_iso_datetime(yc.get('thoi_gian_san_sang'))
        time_delta = (san_sang_at - now) if san_sang_at else None
        request_lat = parse_float(yc.get('lat'))
        request_lng = parse_float(yc.get('lng'))

        urgency_score = 5
        if time_delta is not None:
            hours_until_ready = time_delta.total_seconds() / 3600
            if hours_until_ready <= 24:
                urgency_score = 20
            elif hours_until_ready <= 72:
                urgency_score = 12

        quantity_score = min((so_luong / 1000.0) * 15, 15)

        for kho in kho_list:
            warehouse_lat = parse_float(kho.get('lat'))
            warehouse_lng = parse_float(kho.get('lng'))
            distance_km, distance_score = score_distance_match(request_lat, request_lng, warehouse_lat, warehouse_lng)

            text_location_score = 0
            text_location_score += score_location_match(yc.get('tinh_thanh'), [
                (kho.get('tinh_thanh'), 40),
                (kho.get('dia_chi'), 40)
            ])
            text_location_score += score_location_match(yc.get('quan_huyen'), [
                (kho.get('quan_huyen'), 15),
                (kho.get('dia_chi'), 15)
            ])
            text_location_score += score_location_match(yc.get('xa_phuong'), [
                (kho.get('xa_phuong'), 10),
                (kho.get('dia_chi'), 10)
            ])

            location_score = max(distance_score, text_location_score)

            status_bonus = 0
            trang_thai = normalize_text(kho.get('trang_thai'))
            if trang_thai == 'còn trống':
                status_bonus = 10
            elif trang_thai == 'gần đầy':
                status_bonus = 4

            total_score = max(0, min(100, location_score + urgency_score + quantity_score + status_bonus))

            if not best_match or total_score > best_match['match_score']:
                best_match = {
                    'm_yeucau': yc.get('m_yeucau'),
                    'ma_kho': kho.get('ma_kho'),
                    'ten_kho': kho.get('ten_kho'),
                    'match_score': round(total_score, 1),
                    'suggested_reason': build_warehouse_reason_with_distance(location_score, urgency_score, quantity_score, distance_km)
                }

        if best_match and best_match['match_score'] >= 20:
            results.append(best_match)

    results = sorted(results, key=lambda x: x['match_score'], reverse=True)
    return jsonify({'results': results})


@app.route('/enterprise-stock-matching', methods=['POST'])
def enterprise_stock_matching():
    data = request.json
    stock_lots = data.get('stock_lots', [])
    nhu_cau = data.get('nhu_cau', '')
    tinh_doanh_nghiep = data.get('tinh_doanh_nghiep', '')
    dia_chi_doanh_nghiep = data.get('dia_chi_doanh_nghiep', '')
    enterprise_lat = parse_float(data.get('enterprise_lat'))
    enterprise_lng = parse_float(data.get('enterprise_lng'))

    needs = split_needs(nhu_cau)
    if not stock_lots or not needs:
        return jsonify({'results': []})

    results = []
    enterprise_location = normalize_text(tinh_doanh_nghiep)
    enterprise_address = normalize_text(dia_chi_doanh_nghiep)

    for lot in stock_lots:
        ten_san_pham = normalize_text(lot.get('ten_san_pham'))
        base_match_score = get_best_need_score(ten_san_pham, needs)
        if base_match_score < 35:
            continue

        quantity = float(lot.get('khoi_luong_con_lai', 0) or 0)
        quantity_score = min((quantity / 1000.0) * 10, 10)

        kho_location = normalize_text(lot.get('tinh_thanh'))
        kho_address = normalize_text(lot.get('dia_chi_kho'))
        kho_lat = parse_float(lot.get('lat'))
        kho_lng = parse_float(lot.get('lng'))
        distance_km, distance_score = score_distance_match(enterprise_lat, enterprise_lng, kho_lat, kho_lng)

        province_fuzzy_score = 0
        same_province = False
        if enterprise_location and kho_location:
            province_fuzzy_score = fuzz.partial_ratio(enterprise_location, kho_location)
            same_province = enterprise_location == kho_location

        address_fuzzy_score = fuzz.partial_ratio(enterprise_address, kho_address) if enterprise_address and kho_address else 0
        location_score = score_enterprise_location(distance_km, same_province, province_fuzzy_score, address_fuzzy_score)

        # Với doanh nghiệp, điểm khớp nhu cầu chỉ là một phần. Kho quá xa phải bị hạ điểm rõ ràng.
        total_score = max(0, min(100, (base_match_score * 0.65) + quantity_score + location_score + (distance_score * 0.15)))
        if total_score < 40:
            continue

        results.append({
            'm_yeucau': lot.get('m_yeucau'),
            'ma_san_pham': lot.get('ma_san_pham'),
            'ma_lo_ton_kho': lot.get('ma_lo_ton_kho'),
            'ma_kho': lot.get('ma_kho'),
            'ten_kho': lot.get('ten_kho'),
            'ten_san_pham': lot.get('ten_san_pham'),
            'khoi_luong_con_lai': round(quantity, 2),
            'don_vi_tinh': lot.get('don_vi_tinh'),
            'dia_chi_kho': lot.get('dia_chi_kho'),
            'distance_km': round(distance_km, 2) if distance_km is not None else None,
            'match_score': round(total_score, 1)
        })

    results = sorted(results, key=lambda x: x['match_score'], reverse=True)
    return jsonify({'results': results})

if __name__ == '__main__':
    app.run(host="0.0.0.0", port=5000)