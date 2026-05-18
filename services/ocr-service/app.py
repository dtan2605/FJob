from flask import Flask, request, jsonify
import pytesseract
from PIL import Image
from pdf2image import convert_from_bytes
import io

app = Flask(__name__)

@app.route('/api/ocr/parse', methods=['POST'])
def parse_ocr():
    if 'file' not in request.files:
        return jsonify({'message': 'No file provided'}), 400

    f = request.files['file']
    filename = f.filename.lower()
    data = f.read()

    try:
        if filename.endswith('.pdf'):
            images = convert_from_bytes(data)
            texts = []
            for img in images:
                texts.append(pytesseract.image_to_string(img, lang='eng+vie'))
            text = '\n\n'.join(texts)
        else:
            # Try opening as image
            img = Image.open(io.BytesIO(data))
            text = pytesseract.image_to_string(img, lang='eng+vie')
    except Exception as e:
        return jsonify({'message': 'OCR failed', 'error': str(e)}), 500

    return jsonify({'text': text})
