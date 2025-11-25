namespace MES.Server.Pages
{
    public class OrderModel : PageModel
    {
        private readonly OrderService _orderService;

        [BindProperty]
        public int RequestQty { get; set; } = 1; // 기본값 설정

        public string? Message { get; private set; }

        public OrderModel(OrderService orderService)
        {
            _orderService = orderService;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                _orderService.CreateWebOrder(RequestQty);
                Message = $"주문이 요청되었습니다. (수량: {RequestQty})";
                // 성공적으로 처리 후, 같은 페이지에 머무르면서 메시지를 보여준다.
                return Page();
            }
            catch (Exception ex)
            {
                // 로깅은 OrderService에서 이미 처리했을 것으로 예상
                ModelState.AddModelError(string.Empty, "주문 처리 중 오류가 발생했습니다. 잠시 후 다시 시도해주세요.");
                return Page();
            }
        }
    }
}
