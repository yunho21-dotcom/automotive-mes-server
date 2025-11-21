// 주문 정보를 담는 모델 클래스
public class OrderModel
{
    public int OrderId { get; set; }      // DB의 고유 번호
    public int RequestQty { get; set; }   // D310: 요청 수량
    public int TargetQty { get; set; }    // D315: 지시 수량
    public DateTime OrderTime { get; set; }
}