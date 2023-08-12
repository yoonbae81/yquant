using yQuant.Domain.Entities;

namespace yQuant.Application.Ports.Out; 
internal interface LoadAccountPort {
    Account LoadAccount();
}
